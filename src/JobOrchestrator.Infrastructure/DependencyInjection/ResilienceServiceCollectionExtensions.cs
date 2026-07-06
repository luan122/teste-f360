using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobOrchestrator.Infrastructure.DependencyInjection;

/// <summary>Registers the Polly-backed external-dependency gateway and its configuration.</summary>
public static class ResilienceServiceCollectionExtensions
{
    public const string HttpClientName = "external-deps";

    public static IServiceCollection AddResilience(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ResilienceOptions>(configuration.GetSection(ResilienceOptions.SectionName));

        services.AddSingleton<IExternalDependencyGateway>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ResilienceOptions>>();
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.Value.ExternalDependencyBaseUrl, UriKind.Absolute),
            };
            return new PollyExternalDependencyGateway(httpClient, options);
        });

        return services;
    }
}
