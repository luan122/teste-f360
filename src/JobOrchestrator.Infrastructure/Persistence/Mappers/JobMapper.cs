using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Infrastructure.Persistence.Documents;
using MongoDB.Bson;

namespace JobOrchestrator.Infrastructure.Persistence.Mappers;

public static class JobMapper
{
    public static JobDocument ToDocument(Job job) => new()
    {
        JobId = job.JobId,
        IdempotencyKey = job.IdempotencyKey,
        Type = job.Type,
        Payload = BsonDocument.Parse(job.Payload),
        Priority = job.Priority.ToString(),
        Status = job.Status.ToString(),
        ScheduledAt = job.ScheduledAt?.UtcDateTime,
        Attempts = job.Attempts,
        MaxAttempts = job.MaxAttempts,
        CorrelationId = job.CorrelationId,
        Result = job.Result is null ? null : BsonDocument.Parse(job.Result),
        Error = job.Error,
        CreatedAt = job.CreatedAt.UtcDateTime,
        UpdatedAt = job.UpdatedAt.UtcDateTime,
    };

    public static Job ToDomain(JobDocument document) => Job.Rehydrate(
        jobId: document.JobId,
        idempotencyKey: document.IdempotencyKey,
        type: document.Type,
        payload: document.Payload.ToJson(),
        priority: Enum.Parse<Priority>(document.Priority),
        status: Enum.Parse<JobStatus>(document.Status),
        scheduledAt: document.ScheduledAt is { } scheduledAt ? new DateTimeOffset(scheduledAt, TimeSpan.Zero) : null,
        attempts: document.Attempts,
        maxAttempts: document.MaxAttempts,
        correlationId: document.CorrelationId,
        result: document.Result?.ToJson(),
        error: document.Error,
        createdAt: new DateTimeOffset(document.CreatedAt, TimeSpan.Zero),
        updatedAt: new DateTimeOffset(document.UpdatedAt, TimeSpan.Zero));
}
