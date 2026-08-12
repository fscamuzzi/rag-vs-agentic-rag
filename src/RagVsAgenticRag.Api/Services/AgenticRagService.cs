using System.Diagnostics;
using Microsoft.Extensions.AI;
using RagVsAgenticRag.Api.Models;

namespace RagVsAgenticRag.Api.Services;

/// <summary>
/// Agentic RAG: the LLM drives retrieval through tool calling. It decides
/// what to search, judges whether the evidence is enough, and iterates with
/// refined queries when it is not.
/// </summary>
public class AgenticRagService(
    IChatClient chatClient,
    VectorSearchService vectorSearchService,
    RunLogService runLogService,
    ILoggerFactory loggerFactory)
{
    private const string SystemPrompt =
        """
        You are a retrieval agent for the Nordwind Outdoor knowledge base.

        Strategy:
        1. Break the user's question into one or more focused search queries and
           call search_docs for each aspect of the question.
        2. Call rerank_evaluate to verify the retrieved chunks are sufficient.
        3. If INSUFFICIENT, call refine_query and search again with the
           suggested queries. Repeat at most twice.
        4. Only when the evidence is sufficient, write the final answer.
           Cite sources as [file#chunk].

        Rules:
        - Never answer from general knowledge: if the knowledge base does not
          cover the question, say exactly that.
        - When policies differ by year or by customer type, retrieve BOTH sides
          before comparing.
        """;

    public async Task<RagAnswer> AskAsync(AskRequest request, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = new AgentToolContext(
            vectorSearchService, chatClient, loggerFactory.CreateLogger("Agent"));

        var options = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(context.SearchDocsAsync, "search_docs"),
                AIFunctionFactory.Create(context.RerankEvaluateAsync, "rerank_evaluate"),
                AIFunctionFactory.Create(context.RefineQueryAsync, "refine_query")
            ]
        };

        List<ChatMessage> messages =
        [
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, request.Question)
        ];

        // UseFunctionInvocation() runs the loop: model -> tool -> model -> ...
        var response = await chatClient.GetResponseAsync(messages, options, ct);
        stopwatch.Stop();

        var llmCalls = response.Messages.Count(m => m.Role == ChatRole.Assistant)
                       + context.ExtraLlmCalls;

        var answer = new RagAnswer(
            Mode: "agentic",
            Question: request.Question,
            Answer: response.Text,
            Sources: context.Retrieved
                .OrderByDescending(c => c.Score)
                .Select(c => new RagSource(c.Source, c.ChunkIndex, c.Score))
                .ToList(),
            Trace: context.Trace,
            Metrics: new RagMetrics(
                stopwatch.ElapsedMilliseconds,
                (response.Usage?.InputTokenCount ?? 0) + context.ExtraInputTokens,
                (response.Usage?.OutputTokenCount ?? 0) + context.ExtraOutputTokens,
                llmCalls,
                context.Trace.Count));

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
