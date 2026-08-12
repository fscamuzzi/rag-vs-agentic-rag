using System.Text.Json;
using RagVsAgenticRag.Api.Models;
using RagVsAgenticRag.Api.Services;

namespace RagVsAgenticRag.Api.Endpoints;

public static class BenchmarkEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapBenchmarkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/benchmark").WithTags("Benchmark");

        group.MapPost("/run", RunBenchmarkAsync)
            .WithSummary("Runs every benchmark question through both pipelines and stores the report.")
            .Produces<BenchmarkRunDocument>();

        group.MapGet("/latest",
                async (RunLogService runLogService, CancellationToken ct) =>
                    await runLogService.GetLatestBenchmarkAsync(ct) is { } report
                        ? Results.Ok(report)
                        : Results.NotFound())
            .WithSummary("Returns the most recent benchmark report.")
            .Produces<BenchmarkRunDocument>();

        return app;
    }

    private static async Task<IResult> RunBenchmarkAsync(
        NaiveRagService naiveService,
        AgenticRagService agenticService,
        RunLogService runLogService,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Benchmark");
        var questionsPath = Path.Combine(AppContext.BaseDirectory, "data", "benchmark-questions.json");
        var questions = JsonSerializer.Deserialize<List<BenchmarkQuestion>>(
            await File.ReadAllTextAsync(questionsPath, ct), JsonOptions) ?? [];

        var entries = new List<BenchmarkEntry>();
        foreach (var question in questions)
        {
            logger.LogInformation("BENCHMARK {Id} ({Category})", question.Id, question.Category);
            var request = new AskRequest(question.Question);

            var naive = await naiveService.AskAsync(request, ct);
            entries.Add(ToEntry(question, naive));

            var agentic = await agenticService.AskAsync(request, ct);
            entries.Add(ToEntry(question, agentic));
        }

        var report = new BenchmarkRunDocument
        {
            ChatModel = ResolveChatModel(configuration),
            Entries = entries
        };

        await runLogService.SaveBenchmarkAsync(report, ct);
        return Results.Ok(report);
    }

    private static BenchmarkEntry ToEntry(BenchmarkQuestion question, RagAnswer answer) =>
        new(question.Id, question.Category, question.Question,
            answer.Mode, answer.Answer, answer.Metrics);

    private static string ResolveChatModel(IConfiguration configuration) =>
        (configuration["AI:Provider"] ?? "ollama").Equals("openai", StringComparison.OrdinalIgnoreCase)
            ? configuration["AI:OpenAI:ChatModel"] ?? "unknown"
            : configuration["AI:Ollama:ChatModel"] ?? "unknown";
}
