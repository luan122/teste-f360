using JobOrchestrator.IntegrationTests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace JobOrchestrator.IntegrationTests.IngestionApi;

/// <summary>
/// Boots the real Api host (specs/002-ingestion-api) against a Testcontainers-provided MongoDB
/// replica set. Overrides "Mongo:ConnectionString" so the composition root's own
/// <c>AddInfrastructure</c> registrations point at the container instead of local dev Mongo.
/// </summary>
public sealed class IngestionApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7.0").WithReplicaSet().Build();

    public const string ApiKey = "test-api-key";

    public string DatabaseName { get; } = $"joborchestrator_test_{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        await MongoReplicaSetReadiness.WaitUntilPrimaryIsWritableAsync(GetDatabase());
    }

    public new async Task DisposeAsync()
    {
        await _mongo.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = BuildDirectConnectionString(),
                ["Mongo:DatabaseName"] = DatabaseName,
                ["ApiKeys:Keys:0"] = ApiKey,
            });
        });

        builder.ConfigureServices(services =>
        {
            // Nothing to override — AddInfrastructure/AddIngestionApi read the config above.
        });
    }

    public IMongoDatabase GetDatabase()
    {
        var client = new MongoClient(BuildDirectConnectionString());
        return client.GetDatabase(DatabaseName);
    }

    // Testcontainers remaps the host port; the container's self-advertised replica-set member
    // address won't match unless directConnection is forced (see MongoUnitOfWorkTests). Routed
    // through MongoUrlBuilder rather than raw string concatenation so this is correct whether or
    // not the base connection string already carries query parameters.
    private string BuildDirectConnectionString()
    {
        var urlBuilder = new MongoUrlBuilder(_mongo.GetConnectionString()) { DirectConnection = true };
        return urlBuilder.ToString();
    }
}
