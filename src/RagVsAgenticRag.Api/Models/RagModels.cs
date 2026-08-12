using System.ComponentModel.DataAnnotations;

namespace RagVsAgenticRag.Api.Models;

public record AskRequest(
    [property: Required, MinLength(3)] string Question,
    [property: Range(1, 50)] int? TopK = null);

public record RagSource(string Source, int ChunkIndex, float Score);

public record RagMetrics(
    long DurationMs,
    long InputTokens,
    long OutputTokens,
    int LlmCalls,
    int ToolCalls);

public record ToolTraceEntry(
    int Step,
    string Tool,
    string Arguments,
    string ResultSummary,
    long DurationMs);

public record RagAnswer(
    string Mode,
    string Question,
    string Answer,
    IReadOnlyList<RagSource> Sources,
    IReadOnlyList<ToolTraceEntry> Trace,
    RagMetrics Metrics);

public record ScoredChunk(
    Guid Id,
    string Source,
    int ChunkIndex,
    string Text,
    float Score);

public record SeedResult(int Documents, int Chunks, string Collection);
