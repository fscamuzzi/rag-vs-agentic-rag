using System.Diagnostics;
using Microsoft.Extensions.AI;
using RagVsAgenticRag.Api.Models;

namespace RagVsAgenticRag.Api.Services;

/// <summary>
/// Classic linear RAG: embed the question, retrieve once, stuff everything
/// into the prompt, answer. No retries, no self-evaluation.
/// </summary>
public class NaiveRagService(
    IChatClient chatClient,
    VectorSearchService vectorSearchService,
    RunLogService runLogService,
    IConfiguration configuration,
    ILogger<NaiveRagService> logger)
{
    public async Task<RagAnswer> AskAsync(AskRequest request, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var topK = request.TopK ?? configuration.GetValue("Rag:TopK", 4);

        // 1. Retrieve — one shot, no second chances.
        var chunks = await vectorSearchService.HybridSearchAsync(request.Question, topK, ct);
        logger.LogInformation("NAIVE retrieved {Count} chunks for {Question}", chunks.Count, request.Question);

        // 2. Stuff everything into the prompt.
        var context = string.Join("\n---\n",
            chunks.Select(c => $"[{c.Source}#{c.ChunkIndex}]\n{c.Text}"));

        List<ChatMessage> messages =
        [
            new(ChatRole.System,
                "Answer using ONLY the context below. If the context is not enough " +
                "to answer, say so explicitly. Cite sources as [file#chunk].\n\n" +
                $"Context:\n{context}"),
            new(ChatRole.User, request.Question)
        ];

        // 3. Answer. Whatever was retrieved is all the model will ever see.
        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        stopwatch.Stop();

        var answer = new RagAnswer(
            Mode: "naive",
            Question: request.Question,
            Answer: response.Text,
            Sources: chunks.Select(c => new RagSource(c.Source, c.ChunkIndex, c.Score)).ToList(),
            Trace: [],
            Metrics: new RagMetrics(
                stopwatch.ElapsedMilliseconds,
                response.Usage?.InputTokenCount ?? 0,
                response.Usage?.OutputTokenCount ?? 0,
                LlmCalls: 1,
                ToolCalls: 0));

        await runLogService.SaveRunAsync(ToRunLog(answer), ct);
        return answer;
    }

    private static RunLog ToRunLog(RagAnswer answer) => new()
    {
        Mode = answer.Mode,
        Question = answer.Question,
        Answer = answer.Answer,
        Sources = answer.Sources.ToList(),
        Trace = answer.Trace.ToList(),
        Metrics = answer.Metrics
    };
}
