using System.Text.Json;
using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Application.Contracts;
using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Domain.Outbox;
using MediatR;

namespace JobOrchestrator.Application.Features.Jobs;

/// <summary>Handles job creation with idempotency, transactional outbox, and concurrent-request safety.</summary>
public sealed class CreateJobCommandHandler(
    IJobRepository jobRepository,
    IOutboxRepository outboxRepository,
    IUnitOfWork unitOfWork,
    IIdempotencyStore idempotencyStore,
    IClock clock) : IRequestHandler<CreateJobCommand, CreateJobResult>
{
    public async Task<CreateJobResult> Handle(CreateJobCommand request, CancellationToken cancellationToken)
    {
        var existing = await idempotencyStore.FindAsync(request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ResolveExistingAsync(existing, request.RequestHash, cancellationToken);
        }

        var now = clock.UtcNow;
        var job = Job.Create(
            Guid.NewGuid(),
            request.IdempotencyKey,
            request.Type,
            request.Payload,
            request.Priority,
            request.ScheduledAt,
            request.MaxAttempts,
            request.CorrelationId,
            now);

        if (job.Status == JobStatus.Pending)
        {
            job.MarkQueued(now);
        }

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await jobRepository.AddAsync(job, ct);

            if (job.Status == JobStatus.Queued)
            {
                var payload = JsonSerializer.Serialize(
                    new JobQueued(job.JobId, job.Type, job.Priority.ToString(), job.CorrelationId, job.Attempts));
                var outboxMessage = OutboxMessage.Create(job.JobId, nameof(JobQueued), payload, now);
                await outboxRepository.AddAsync(outboxMessage, ct);
            }

            await idempotencyStore.TryInsertAsync(
                new IdempotencyRecord(request.IdempotencyKey, job.JobId, request.RequestHash, now), ct);
        }, cancellationToken);

        var record = await idempotencyStore.FindAsync(request.IdempotencyKey, cancellationToken);
        if (record is not null && record.JobId != job.JobId)
        {
            return await ResolveExistingAsync(record, request.RequestHash, cancellationToken);
        }

        return CreateJobResult.Success(job);
    }

    private async Task<CreateJobResult> ResolveExistingAsync(
        IdempotencyRecord record, string requestHash, CancellationToken cancellationToken)
    {
        var existingJob = await jobRepository.GetByIdAsync(record.JobId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Idempotency record for key '{record.IdempotencyKey}' points at missing job {record.JobId}.");

        return record.RequestHash == requestHash
            ? CreateJobResult.Success(existingJob)
            : CreateJobResult.IdempotencyConflict(existingJob);
    }
}
