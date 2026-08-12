using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagVsAgenticRag.Api.Extensions;
using Xunit;

namespace RagVsAgenticRag.Api.Tests;

public class InfrastructureServiceExtensionsTests
{
    /// <summary>
    /// Verifies that Ollama requests use the configured endpoint and a timeout suitable for local agent runs.
    /// </summary>
    [Fact]
    public void AddInfrastructureServices_ConfiguresOllamaHttpClient()
    {
        // Build a complete Ollama configuration without contacting external infrastructure.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "ollama",
                ["AI:Ollama:Endpoint"] = "http://localhost:11434",
                ["AI:Ollama:ChatModel"] = "llama3.1:8b",
                ["AI:Ollama:EmbeddingModel"] = "nomic-embed-text",
                ["AI:Ollama:RequestTimeout"] = "00:07:00",
                ["Mongo:ConnectionString"] = "mongodb://localhost:27017",
                ["Mongo:Database"] = "test"
            })
            .Build();

        // Create the service collection that receives the production registrations.
        var services = new ServiceCollection();

        // Register the infrastructure services under test.
        services.AddInfrastructureServices(configuration);

        // Resolve the real named client used by both Ollama adapters.
        using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("Ollama");

        Assert.Equal(new Uri("http://localhost:11434"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromMinutes(7), client.Timeout);
    }

    /// <summary>
    /// Verifies that a non-HTTP Ollama endpoint fails during service registration rather than on the first request.
    /// </summary>
    [Fact]
    public void AddInfrastructureServices_RejectsInvalidOllamaEndpointAtStartup()
    {
        // Build an otherwise valid configuration with an unsupported endpoint scheme.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "ollama",
                ["AI:Ollama:Endpoint"] = "file:///tmp/ollama.sock",
                ["AI:Ollama:ChatModel"] = "llama3.1:8b",
                ["AI:Ollama:EmbeddingModel"] = "nomic-embed-text",
                ["Mongo:ConnectionString"] = "mongodb://localhost:27017",
                ["Mongo:Database"] = "test"
            })
            .Build();
        var services = new ServiceCollection();

        // Registration must reject invalid configuration before the host starts.
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructureServices(configuration));

        Assert.Contains("AI:Ollama:Endpoint", exception.Message);
    }

    /// <summary>
    /// Verifies that a non-positive Ollama request timeout fails during service registration.
    /// </summary>
    [Fact]
    public void AddInfrastructureServices_RejectsInvalidOllamaTimeoutAtStartup()
    {
        // Build an otherwise valid configuration with a timeout that HttpClient cannot use.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "ollama",
                ["AI:Ollama:Endpoint"] = "http://localhost:11434",
                ["AI:Ollama:ChatModel"] = "llama3.1:8b",
                ["AI:Ollama:EmbeddingModel"] = "nomic-embed-text",
                ["AI:Ollama:RequestTimeout"] = "00:00:00",
                ["Mongo:ConnectionString"] = "mongodb://localhost:27017",
                ["Mongo:Database"] = "test"
            })
            .Build();
        var services = new ServiceCollection();

        // Registration must reject invalid configuration before the host starts.
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructureServices(configuration));

        Assert.Contains("AI:Ollama:RequestTimeout", exception.Message);
    }
}
