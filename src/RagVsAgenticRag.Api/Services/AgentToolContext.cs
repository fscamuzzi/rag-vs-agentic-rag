using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using RagVsAgenticRag.Api.Models;

namespace RagVsAgenticRag.Api.Services;

/// <summary>
/// Per-request state shared by the agent tools: everything retrieved so far,
/// the tool trace, and the token cost of the auxiliary LLM calls made inside
/// tools (rerank_evaluate and refine_query call the model themselves).
/// </summary>
public class AgentToolContext(
    VectorSearchService vectorSearchService,
    IChatClient chatClient,
    ILogger logger)
{
    public List<ScoredChunk> Retrieved { get; } = [];
    public List<ToolTraceEntry> Trace { get; } = [];
    public int ExtraLlmCalls { get; private set; }
    public long ExtraInputTokens { get; private set; }
    public long ExtraOutputTokens { get; private set; }

    [Description("Search the company knowledge base with a focused query. " +
                 "Returns the most relevant document chunks. Call it multiple " +
                 "times with different queries for multi-part questions.")]
    public async Task<string> SearchDocsAsync(
        [Description("A short, focused search query")] string query,
        [Description("How many chunks to retrieve")] int topK = 5,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var chunks = await vectorSearchService.HybridSearchAsync(query, topK, ct);

        var newChunks = chunks
            .Where(c => Retrieved.All(r => r.Id != c.Id))
            .ToList();
        Retrieved.AddRange(newChunks);

        var result = chunks.Count == 0
            ? "No results."
            : string.Join("\n---\n",
                chunks.Select(c => $"[{c.Source}#{c.ChunkIndex}] (score {c.Score:F2})\n{c.Text}"));

        RecordStep("search_docs", $"query=\"{query}\" topK={topK}",
            $"{chunks.Count} chunks ({newChunks.Count} new)", stopwatch.ElapsedMilliseconds);

        return result;
    }

    [Description("Evaluate whether the chunks retrieved so far are sufficient " +
                 "to fully answer the user's question. Returns a verdict " +
                 "(SUFFICIENT or INSUFFICIENT) with a short reason.")]
    public async Task<string> RerankEvaluateAsync(
        [Description("The original user question")] string question,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (Retrieved.Count == 0)
        {
            RecordStep("rerank_evaluate", $"question=\"{question}\"",
                "INSUFFICIENT (nothing retrieved)", stopwatch.ElapsedMilliseconds);
            return "INSUFFICIENT: nothing has been retrieved yet. Call search_docs first.";
        }

        var chunkList = string.Join("\n---\n",
            Retrieved.Select((c, i) => $"CHUNK {i} [{c.Source}#{c.ChunkIndex}]\n{c.Text}"));

        List<ChatMessage> messages =
        [
            new(ChatRole.System,
                "You are a strict retrieval judge. Given a question and retrieved " +
                "chunks, decide if the chunks contain ALL the facts needed to answer. " +
                "Reply with exactly one line starting with SUFFICIENT or INSUFFICIENT, " +
                "followed by a one-sentence reason. If insufficient, state what is missing."),
            new(ChatRole.User, $"Question: {question}\n\nChunks:\n{chunkList}")
        ];

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        TrackExtraCall(response);

        RecordStep("rerank_evaluate", $"question=\"{question}\" chunks={Retrieved.Count}",
            response.Text, stopwatch.ElapsedMilliseconds);

        return response.Text;
    }

    [Description("Rewrite a query that returned poor or incomplete results into " +
                 "up to three better search queries.")]
    public async Task<string> RefineQueryAsync(
        [Description("The query that did not work well")] string originalQuery,
        [Description("Why the results were insufficient")] string reason,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        List<ChatMessage> messages =
        [
            new(ChatRole.System,
                "You rewrite search queries for a company knowledge base. Given a " +
                "failed query and the reason it failed, produce 1 to 3 alternative " +
                "queries, one per line, no numbering, no commentary. Use different " +
                "wording, synonyms, or split the query into narrower sub-queries."),
            new(ChatRole.User, $"Failed query: {originalQuery}\nReason: {reason}")
        ];

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        TrackExtraCall(response);

        RecordStep("refine_query", $"original=\"{originalQuery}\"",
            response.Text.ReplaceLineEndings(" | "), stopwatch.ElapsedMilliseconds);

        return response.Text;
    }

    private void TrackExtraCall(ChatResponse response)
    {
        ExtraLlmCalls++;
        ExtraInputTokens += response.Usage?.InputTokenCount ?? 0;
        ExtraOutputTokens += response.Usage?.OutputTokenCount ?? 0;
    }

    private void RecordStep(string tool, string arguments, string resultSummary, long durationMs)
    {
        var step = Trace.Count + 1;
        Trace.Add(new ToolTraceEntry(step, tool, arguments, resultSummary, durationMs));
        logger.LogInformation(
            "AGENT step {Step} | {Tool}({Arguments}) -> {Result} ({DurationMs} ms)",
            step, tool, arguments, resultSummary, durationMs);
    }
}
