using System.Text.Json;
using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Application.Contracts;
using JobOrchestrator.Domain.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobOrchestrator.Infrastructure.Scheduling;

/// <summary>
/// Polls for <c>Scheduled</c> jobs whose <c>ScheduledAt</c> is due, transitions them to <c>Queued</c>,
/// and writes a <see cref="JobQueued"/> outbox message atomically. Never publishes to the broker directly.
/// </summary>
public sealed class ScheduledJobReleaser(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    IOptions<ReleaserOptions> options,
    ILogger<ScheduledJobReleaser> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));

        using var timer = new PeriodicTimer(pollInterval);
        do
        {
            try
            {
                await ReleaseDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled job release poll failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ReleaseDueJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = clock.UtcNow;
        var dueJobs = await jobRepository.GetDueScheduledJobsAsync(now, options.Value.BatchSize, cancellationToken);

        if (dueJobs.Count == 0)
            return;

        var releasedCount = 0;

        foreach (var job in dueJobs)
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                job.MarkQueued(now);
                await jobRepository.UpdateAsync(job, ct);

                var message = new JobQueued(job.JobId, job.Type, job.Priority.ToString(), job.CorrelationId, job.Attempts);
                var payload = JsonSerializer.Serialize(message);
                var outboxMessage = OutboxMessage.Create(job.JobId, nameof(JobQueued), payload, now);
                await outboxRepository.AddAsync(outboxMessage, ct);
            }, cancellationToken);

            releasedCount++;
        }

        if (releasedCount > 0)
        {
            logger.LogInformation("Released {ReleasedCount} scheduled job(s) that became due.", releasedCount);
        }
    }
}
