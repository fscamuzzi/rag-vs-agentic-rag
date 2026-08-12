using Microsoft.Extensions.AI;
using MongoDB.Driver;
using OllamaSharp;
using Qdrant.Client;
using RagVsAgenticRag.Api.Services;

namespace RagVsAgenticRag.Api.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddAiClients(services, configuration);

        services.AddSingleton(_ => new QdrantClient(
            configuration["Qdrant:Host"] ?? "localhost",
            configuration.GetValue("Qdrant:GrpcPort", 6334)));

        services.AddSingleton<IMongoDatabase>(_ =>
            new MongoClient(configuration["Mongo:ConnectionString"])
                .GetDatabase(configuration["Mongo:Database"]));

        services.AddSingleton<SparseEncoder>();
        services.AddSingleton<ChunkingService>();
        services.AddSingleton<VectorSearchService>();
        services.AddSingleton<IngestionService>();
        services.AddSingleton<RunLogService>();
        services.AddSingleton<NaiveRagService>();
        services.AddSingleton<AgenticRagService>();

        return services;
    }

    private static void AddAiClients(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["AI:Provider"] ?? "ollama";
        var maxIterations = configuration.GetValue("Rag:MaxAgentIterations", 5);

        if (provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
        {
            var openAi = new OpenAI.OpenAIClient(configuration["AI:OpenAI:ApiKey"]);

            services.AddChatClient(openAi
                    .GetChatClient(configuration["AI:OpenAI:ChatModel"]!)
                    .AsIChatClient())
                .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = maxIterations);

            services.AddEmbeddingGenerator(openAi
                .GetEmbeddingClient(configuration["AI:OpenAI:EmbeddingModel"]!)
                .AsIEmbeddingGenerator());

            return;
        }

        var endpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";

        services.AddChatClient(
                (IChatClient)new OllamaApiClient(endpoint, configuration["AI:Ollama:ChatModel"]!))
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = maxIterations);

        services.AddEmbeddingGenerator(
            (IEmbeddingGenerator<string, Embedding<float>>)new OllamaApiClient(
                endpoint, configuration["AI:Ollama:EmbeddingModel"]!));
    }
}
