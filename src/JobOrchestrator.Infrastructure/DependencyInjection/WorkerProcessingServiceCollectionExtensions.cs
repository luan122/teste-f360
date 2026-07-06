using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Infrastructure.Jobs;
using JobOrchestrator.Infrastructure.Messaging;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobOrchestrator.Infrastructure.DependencyInjection;

/// <summary>Registers the MassTransit/RabbitMQ bus, priority queue, message retry, outbox dispatcher, and job handler registry.</summary>
public static class WorkerProcessingServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerProcessing(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutboxDispatcherOptions>(configuration.GetSection(OutboxDispatcherOptions.SectionName));

        services.AddSingleton<IJobHandlerRegistry, JobHandlerRegistry>();
        services.AddSingleton<IJobHandler, DemoJobHandler>();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<JobQueuedConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var rabbitOptions = ctx.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

                cfg.Host(rabbitOptions.Host, rabbitOptions.VirtualHost, h =>
                {
                    h.Username(rabbitOptions.Username);
                    h.Password(rabbitOptions.Password);
                });

                cfg.ReceiveEndpoint("jobs-queued", e =>
                {
                    e.ConfigureConsumer<JobQueuedConsumer>(ctx);
                    e.SetQueueArgument("x-max-priority", 10);
                    e.UseMessageRetry(r => r.Exponential(
                        5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
                });
            });
        });

        services.AddHostedService<OutboxDispatcher>();

        return services;
    }
}
