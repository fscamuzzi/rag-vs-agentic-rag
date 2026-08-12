using MongoDB.Driver;
using RagVsAgenticRag.Api.Models;

namespace RagVsAgenticRag.Api.Services;

public class RunLogService(IMongoDatabase database)
{
    private readonly IMongoCollection<RunLog> _runs =
        database.GetCollection<RunLog>("runs");

    private readonly IMongoCollection<BenchmarkRunDocument> _benchmarks =
        database.GetCollection<BenchmarkRunDocument>("benchmarks");

    public Task SaveRunAsync(RunLog run, CancellationToken ct = default) =>
        _runs.InsertOneAsync(run, cancellationToken: ct);

    public Task SaveBenchmarkAsync(BenchmarkRunDocument report, CancellationToken ct = default) =>
        _benchmarks.InsertOneAsync(report, cancellationToken: ct);

    public async Task<BenchmarkRunDocument?> GetLatestBenchmarkAsync(CancellationToken ct = default) =>
        await _benchmarks
            .Find(FilterDefinition<BenchmarkRunDocument>.Empty)
            .SortByDescending(b => b.RanAt)
            .FirstOrDefaultAsync(ct);
}
