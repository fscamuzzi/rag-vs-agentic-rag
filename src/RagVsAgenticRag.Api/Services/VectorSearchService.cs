using Microsoft.Extensions.AI;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagVsAgenticRag.Api.Models;

namespace RagVsAgenticRag.Api.Services;

/// <summary>
/// Qdrant hybrid search: dense (embeddings) + sparse (BM25-style) vectors,
/// fused server-side with Reciprocal Rank Fusion.
/// </summary>
public class VectorSearchService(
    QdrantClient qdrant,
    SparseEncoder sparseEncoder,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IConfiguration configuration,
    ILogger<VectorSearchService> logger)
{
    private const string DenseVectorName = "dense";
    private const string SparseVectorName = "sparse";

    private readonly string _collection = configuration["Qdrant:Collection"] ?? "nordwind-docs";
    private readonly ulong _vectorSize = configuration.GetValue<ulong>("Qdrant:VectorSize", 768);

    public async Task RecreateCollectionAsync(CancellationToken ct = default)
    {
        if (await qdrant.CollectionExistsAsync(_collection, ct))
            await qdrant.DeleteCollectionAsync(_collection, cancellationToken: ct);

        await qdrant.CreateCollectionAsync(
            _collection,
            vectorsConfig: new VectorParamsMap
            {
                Map =
                {
                    [DenseVectorName] = new VectorParams
                    {
                        Size = _vectorSize,
                        Distance = Distance.Cosine
                    }
                }
            },
            sparseVectorsConfig: new SparseVectorConfig
            {
                Map =
                {
                    [SparseVectorName] = new SparseVectorParams { Modifier = Modifier.Idf }
                }
            },
            cancellationToken: ct);

        logger.LogInformation("Recreated Qdrant collection {Collection}", _collection);
    }

    public async Task UpsertAsync(IReadOnlyList<Chunk> chunks, CancellationToken ct = default)
    {
        var embeddings = await embeddingGenerator.GenerateAsync(
            chunks.Select(c => c.Text).ToList(), cancellationToken: ct);

        var points = chunks
            .Zip(embeddings, BuildPoint)
            .ToList();

        await qdrant.UpsertAsync(_collection, points, cancellationToken: ct);
        logger.LogInformation("Upserted {Count} chunks into {Collection}", points.Count, _collection);
    }

    public async Task<IReadOnlyList<ScoredChunk>> HybridSearchAsync(
        string query, int limit, CancellationToken ct = default)
    {
        var denseVector = await embeddingGenerator.GenerateVectorAsync(query, cancellationToken: ct);
        var (indices, values) = sparseEncoder.Encode(query);

        var results = await qdrant.QueryAsync(
            _collection,
            prefetch:
            [
                new PrefetchQuery
                {
                    Query = denseVector.ToArray(),
                    Using = DenseVectorName,
                    Limit = (ulong)(limit * 4)
                },
                new PrefetchQuery
                {
                    Query = (values, indices),
                    Using = SparseVectorName,
                    Limit = (ulong)(limit * 4)
                }
            ],
            query: Fusion.Rrf,
            limit: (ulong)limit,
            payloadSelector: true,
            cancellationToken: ct);

        return results
            .Select(p => new ScoredChunk(
                Guid.Parse(p.Id.Uuid),
                p.Payload["source"].StringValue,
                (int)p.Payload["chunk_index"].IntegerValue,
                p.Payload["text"].StringValue,
                p.Score))
            .ToList();
    }

    private PointStruct BuildPoint(Chunk chunk, Embedding<float> embedding)
    {
        var (indices, values) = sparseEncoder.Encode(chunk.Text);

        return new PointStruct
        {
            Id = Guid.NewGuid(),
            Vectors = new Dictionary<string, Vector>
            {
                [DenseVectorName] = embedding.Vector.ToArray(),
                [SparseVectorName] = (values, indices)
            },
            Payload =
            {
                ["source"] = chunk.Source,
                ["chunk_index"] = chunk.Index,
                ["text"] = chunk.Text
            }
        };
    }
}
