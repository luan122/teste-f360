using JobOrchestrator.Domain.Jobs;
using MediatR;

namespace JobOrchestrator.Application.Features.Jobs;

/// <summary>Submits a new job for durable processing.</summary>
public sealed record CreateJobCommand(
    string IdempotencyKey,
    string RequestHash,
    string Type,
    Priority Priority,
    string Payload,
    DateTimeOffset? ScheduledAt,
    int MaxAttempts,
    string CorrelationId) : IRequest<CreateJobResult>;

/// <summary>Outcome of <see cref="CreateJobCommand"/>.</summary>
public sealed record CreateJobResult(Job Job, bool IsIdempotencyConflict)
{
    public static CreateJobResult Success(Job job) => new(job, false);

    public static CreateJobResult IdempotencyConflict(Job existingJob) => new(existingJob, true);
}
