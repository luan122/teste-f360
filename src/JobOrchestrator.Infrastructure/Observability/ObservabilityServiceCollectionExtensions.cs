using JobOrchestrator.Infrastructure.Messaging;
using JobOrchestrator.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace JobOrchestrator.Infrastructure.Observability;

/// <summary>
/// Registers OpenTelemetry tracing and TCP-based readiness health checks for MongoDB and RabbitMQ.
/// Consumed by both host composition roots. Callers map health endpoints themselves.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    private const string ReadyTag = "ready";
    private const int DefaultMongoPort = 27017;
    private const int DefaultRabbitMqPort = 5672;

    /// <summary>Registers OpenTelemetry tracing and Mongo/RabbitMQ readiness health checks.</summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddOpenTelemetry()
            .WithTracing(b =>
            {
                b.AddSource(serviceName)
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter();

                configureTracing?.Invoke(b);
            });

        services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                "mongodb",
                sp => new TcpReachabilityHealthCheck(() =>
                {
                    var connectionString = sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString;
                    return ParseHostAndPort(connectionString, DefaultMongoPort);
                }),
                failureStatus: null,
                tags: [ReadyTag]))
            .Add(new HealthCheckRegistration(
                "rabbitmq",
                sp => new TcpReachabilityHealthCheck(() =>
                {
                    var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                    return (options.Host, DefaultRabbitMqPort);
                }),
                failureStatus: null,
                tags: [ReadyTag]));

        return services;
    }

    private static (string Host, int Port) ParseHostAndPort(string connectionString, int defaultPort)
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            return (uri.Host, uri.Port > 0 ? uri.Port : defaultPort);
        }

        return ("localhost", defaultPort);
    }
}
