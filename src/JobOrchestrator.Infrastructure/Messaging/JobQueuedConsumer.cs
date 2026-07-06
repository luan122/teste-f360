using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Application.Contracts;
using JobOrchestrator.Domain.Jobs;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace JobOrchestrator.Infrastructure.Messaging;

/// <summary>
/// Consumes <see cref="JobQueued"/>: atomically claims the job, runs the registered handler, and
/// persists the outcome before the message is acknowledged. Redelivery of a message for an
/// already-claimed or terminal job is an idempotent no-op.
/// </summary>
public sealed class JobQueuedConsumer(
    IJobClaimService jobClaimService,
    IJobRepository jobRepository,
    IJobHandlerRegistry jobHandlerRegistry,
    ICancellationRegistry cancellationRegistry,
    IClock clock,
    ILogger<JobQueuedConsumer> logger) : IConsumer<JobQueued>
{
    public async Task Consume(ConsumeContext<JobQueued> context)
    {
        var jobId = context.Message.JobId;

        var claimed = await jobClaimService.TryClaimAsync(jobId, clock.UtcNow, context.CancellationToken);
        if (!claimed)
        {
            logger.LogInformation(
                "Job {JobId} could not be claimed (already processing or in a terminal state); acking as no-op.",
                jobId);
            return;
        }

        var job = await jobRepository.GetByIdAsync(jobId, context.CancellationToken);
        if (job is null)
        {
            logger.LogError("Claimed job {JobId} was not found in the repository.", jobId);
            return;
        }

        var token = cancellationRegistry.Register(job.JobId, context.CancellationToken);
        try
        {
            var handler = jobHandlerRegistry.Resolve(job.Type);
            var result = await handler.HandleAsync(job, token);
            job.Complete(result, clock.UtcNow);
            await jobRepository.UpdateAsync(job, context.CancellationToken);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            job.Cancel(clock.UtcNow);
            await jobRepository.UpdateAsync(job, context.CancellationToken);
        }
        catch (PermanentJobException ex)
        {
            logger.LogWarning(ex, "Job {JobId} failed permanently: {Reason}", job.JobId, ex.Message);
            job.RecordPermanentFailure(ex.Message, clock.UtcNow);
            await jobRepository.UpdateAsync(job, context.CancellationToken);
        }
        catch (Exception ex)
        {
            var outcome = job.RecordFailure(ex.Message, clock.UtcNow);
            await jobRepository.UpdateAsync(job, context.CancellationToken);

            if (outcome == JobFailureOutcome.Requeued)
            {
                throw;
            }
        }
        finally
        {
            cancellationRegistry.Release(job.JobId);
        }
    }
}
