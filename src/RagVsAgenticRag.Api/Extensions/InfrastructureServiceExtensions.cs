using Microsoft.Extensions.AI;
using MongoDB.Driver;
using OllamaSharp;
using Qdrant.Client;
using RagVsAgenticRag.Api.Services;

namespace RagVsAgenticRag.Api.Extensions;

public static class InfrastructureServiceExtensions
{
    private const string OllamaHttpClientName = "Ollama";

    /// <summary>
    /// Registers the AI providers, vector store, database, and application services.
    /// </summary>
    /// <param name="services">The dependency-injection service collection.</param>
    /// <param name="configuration">The application configuration containing provider settings.</param>
    /// <returns>The same service collection for fluent startup registration.</returns>
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

    /// <summary>
    /// Registers the configured chat and embedding clients, including the agent iteration limit.
    /// </summary>
    /// <param name="services">The dependency-injection service collection.</param>
    /// <param name="configuration">The application configuration containing AI provider settings.</param>
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
        var requestTimeout = configuration.GetValue(
            "AI:Ollama:RequestTimeout",
            TimeSpan.FromMinutes(5));

        // Local tool-calling requests can exceed HttpClient's 100-second default timeout.
        services.AddHttpClient(OllamaHttpClientName, client =>
        {
            client.BaseAddress = new Uri(endpoint);
            client.Timeout = requestTimeout;
        });

        services.AddChatClient(
                serviceProvider => (IChatClient)new OllamaApiClient(
                    serviceProvider.GetRequiredService<IHttpClientFactory>()
                        .CreateClient(OllamaHttpClientName),
                    configuration["AI:Ollama:ChatModel"]!))
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = maxIterations);

        services.AddEmbeddingGenerator<string, Embedding<float>>(
            serviceProvider => new OllamaApiClient(
                serviceProvider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(OllamaHttpClientName),
                configuration["AI:Ollama:EmbeddingModel"]!));
    }
}
