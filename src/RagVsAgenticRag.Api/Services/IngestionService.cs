using RagVsAgenticRag.Api.Models;

namespace RagVsAgenticRag.Api.Services;

public class IngestionService(
    ChunkingService chunkingService,
    VectorSearchService vectorSearchService,
    IConfiguration configuration,
    ILogger<IngestionService> logger)
{
    public async Task<SeedResult> SeedAsync(CancellationToken ct = default)
    {
        var docsPath = Path.Combine(AppContext.BaseDirectory, "data", "docs");
        var files = Directory.GetFiles(docsPath, "*.md");

        var chunks = files
            .SelectMany(f => chunkingService.ChunkMarkdown(
                Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();

        await vectorSearchService.RecreateCollectionAsync(ct);
        await vectorSearchService.UpsertAsync(chunks, ct);

        logger.LogInformation(
            "Seeded {Documents} documents as {Chunks} chunks", files.Length, chunks.Count);

        return new SeedResult(
            files.Length, chunks.Count, configuration["Qdrant:Collection"] ?? "nordwind-docs");
    }
}
