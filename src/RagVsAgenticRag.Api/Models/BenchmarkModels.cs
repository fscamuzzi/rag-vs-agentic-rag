namespace RagVsAgenticRag.Api.Models;

public record BenchmarkQuestion(string Id, string Category, string Question);

public record BenchmarkEntry(
    string QuestionId,
    string Category,
    string Question,
    string Mode,
    string Answer,
    RagMetrics Metrics);

public record BenchmarkReport(
    DateTime RanAt,
    string ChatModel,
    IReadOnlyList<BenchmarkEntry> Entries);
