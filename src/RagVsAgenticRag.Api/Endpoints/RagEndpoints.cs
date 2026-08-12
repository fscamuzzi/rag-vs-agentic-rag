using RagVsAgenticRag.Api.Filters;
using RagVsAgenticRag.Api.Models;
using RagVsAgenticRag.Api.Services;

namespace RagVsAgenticRag.Api.Endpoints;

public static class RagEndpoints
{
    public static IEndpointRouteBuilder MapRagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rag").WithTags("RAG");

        group.MapPost("/naive",
                async (AskRequest request, NaiveRagService service, CancellationToken ct) =>
                    Results.Ok(await service.AskAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<AskRequest>>()
            .WithSummary("Classic linear RAG: embed, retrieve once, answer.")
            .Produces<RagAnswer>();

        group.MapPost("/agentic",
                async (AskRequest request, AgenticRagService service, CancellationToken ct) =>
                    Results.Ok(await service.AskAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<AskRequest>>()
            .WithSummary("Agentic RAG: the LLM drives retrieval via tool calling.")
            .Produces<RagAnswer>();

        return app;
    }
}
