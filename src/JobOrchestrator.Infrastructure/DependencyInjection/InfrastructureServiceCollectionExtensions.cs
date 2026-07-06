using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Infrastructure.Jobs;
using JobOrchestrator.Infrastructure.Messaging;
using JobOrchestrator.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace JobOrchestrator.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared persistence spine (Mongo client/database, repositories, unit of
    /// work, clock) used by every feature. Feature-owned services (messaging, resilience,
    /// observability, handlers) register themselves via their own <c>AddXxx()</c> extension so
    /// this file never needs to change when a feature is added.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return sp.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName);
        });

        services.AddSingleton<IMongoSessionAccessor, MongoSessionAccessor>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
        services.AddScoped<IJobRepository, MongoJobRepository>();
        services.AddScoped<IOutboxRepository, MongoOutboxRepository>();
        services.AddScoped<IJobClaimService, MongoJobClaimService>();
        services.AddScoped<IIdempotencyStore, MongoIdempotencyStore>();
        services.AddSingleton<ICancellationRegistry, CancellationRegistry>();

        services.AddHostedService<MongoIndexInitializer>();

        return services;
    }
}
