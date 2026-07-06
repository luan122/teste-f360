using JobOrchestrator.Infrastructure.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobOrchestrator.Infrastructure.DependencyInjection;

/// <summary>Registers the scheduled-job release background service and its configuration.</summary>
public static class TaskManagementServiceCollectionExtensions
{
    public static IServiceCollection AddTaskManagement(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReleaserOptions>(configuration.GetSection(ReleaserOptions.SectionName));
        services.AddHostedService<ScheduledJobReleaser>();

        return services;
    }
}
