using RagVsAgenticRag.Api.Models;
using RagVsAgenticRag.Api.Services;

namespace RagVsAgenticRag.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin").WithTags("Admin");

        group.MapPost("/seed",
                async (IngestionService service, CancellationToken ct) =>
                    Results.Ok(await service.SeedAsync(ct)))
            .WithSummary("Recreates the Qdrant collection and ingests data/docs.")
            .Produces<SeedResult>();

        return app;
    }
}
