using JobOrchestrator.Domain.Exceptions;

namespace JobOrchestrator.Domain.Jobs;

/// <summary>Aggregate root representing a unit of work submitted to the orchestrator.</summary>
public sealed class Job
{
    private Job(
        Guid jobId,
        string idempotencyKey,
        string type,
        string payload,
        Priority priority,
        JobStatus status,
        DateTimeOffset? scheduledAt,
        int maxAttempts,
        string correlationId,
        DateTimeOffset createdAt)
    {
        JobId = jobId;
        IdempotencyKey = idempotencyKey;
        Type = type;
        Payload = payload;
        Priority = priority;
        Status = status;
        ScheduledAt = scheduledAt;
        MaxAttempts = maxAttempts;
        CorrelationId = correlationId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid JobId { get; }
    public string IdempotencyKey { get; }
    public string Type { get; }
    public string Payload { get; }
    public Priority Priority { get; }
    public JobStatus Status { get; private set; }
    public DateTimeOffset? ScheduledAt { get; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; }
    public string CorrelationId { get; }
    public string? Result { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Creates a new job in <see cref="JobStatus.Scheduled"/> or <see cref="JobStatus.Pending"/> depending on <paramref name="scheduledAt"/>.</summary>
    public static Job Create(
        Guid jobId,
        string idempotencyKey,
        string type,
        string payload,
        Priority priority,
        DateTimeOffset? scheduledAt,
        int maxAttempts,
        string correlationId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Job type is required.", nameof(type));
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Max attempts must be at least 1.");
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation id is required.", nameof(correlationId));

        var status = scheduledAt.HasValue && scheduledAt.Value > now ? JobStatus.Scheduled : JobStatus.Pending;

        return new Job(jobId, idempotencyKey, type, payload, priority, status, scheduledAt, maxAttempts, correlationId, now);
    }

    /// <summary>Reconstitutes a job from persisted state. Bypasses invariant checks that only apply at creation time.</summary>
    public static Job Rehydrate(
        Guid jobId,
        string idempotencyKey,
        string type,
        string payload,
        Priority priority,
        JobStatus status,
        DateTimeOffset? scheduledAt,
        int attempts,
        int maxAttempts,
        string correlationId,
        string? result,
        string? error,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var job = new Job(jobId, idempotencyKey, type, payload, priority, status, scheduledAt, maxAttempts, correlationId, createdAt)
        {
            Attempts = attempts,
            Result = result,
            Error = error,
            UpdatedAt = updatedAt,
        };
        return job;
    }

    /// <summary>True when the job has no schedule, or its schedule has arrived.</summary>
    public bool IsDue(DateTimeOffset now) => !ScheduledAt.HasValue || ScheduledAt.Value <= now;

    /// <summary>Marks a due job as queued for delivery, transitioning it out of the outbox pipeline stage.</summary>
    public void MarkQueued(DateTimeOffset now)
    {
        EnsureStatus(nameof(MarkQueued), JobStatus.Pending, JobStatus.Scheduled);
        Status = JobStatus.Queued;
        UpdatedAt = now;
    }

    /// <summary>Claims the job for processing. Callers must guarantee this is invoked under an atomic claim.</summary>
    public void StartProcessing(DateTimeOffset now)
    {
        EnsureStatus(nameof(StartProcessing), JobStatus.Queued);
        Status = JobStatus.Processing;
        Attempts++;
        UpdatedAt = now;
    }

    public void Complete(string? result, DateTimeOffset now)
    {
        EnsureStatus(nameof(Complete), JobStatus.Processing);
        Status = JobStatus.Completed;
        Result = result;
        UpdatedAt = now;
    }

    /// <summary>Records a failed attempt and decides, from the retry budget, whether to requeue or dead-letter.</summary>
    public JobFailureOutcome RecordFailure(string error, DateTimeOffset now)
    {
        EnsureStatus(nameof(RecordFailure), JobStatus.Processing);
        Error = error;
        UpdatedAt = now;

        if (Attempts >= MaxAttempts)
        {
            Status = JobStatus.DeadLettered;
            return JobFailureOutcome.DeadLettered;
        }

        Status = JobStatus.Queued;
        return JobFailureOutcome.Requeued;
    }

    /// <summary>Dead-letters immediately, bypassing the retry budget. For unrecoverable failures where no retry could succeed.</summary>
    public void RecordPermanentFailure(string error, DateTimeOffset now)
    {
        EnsureStatus(nameof(RecordPermanentFailure), JobStatus.Processing);
        Error = error;
        Status = JobStatus.DeadLettered;
        UpdatedAt = now;
    }

    /// <summary>Requests cancellation. Idempotent when already <see cref="JobStatus.Cancelled"/>; illegal from other terminal statuses.</summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status == JobStatus.Cancelled)
            return;

        if (Status is JobStatus.Completed or JobStatus.DeadLettered)
            throw new InvalidJobStateTransitionException(JobId, Status, nameof(Cancel));

        Status = JobStatus.Cancelled;
        UpdatedAt = now;
    }

    private void EnsureStatus(string attemptedAction, params ReadOnlySpan<JobStatus> allowed)
    {
        foreach (var status in allowed)
        {
            if (Status == status)
                return;
        }

        throw new InvalidJobStateTransitionException(JobId, Status, attemptedAction);
    }
}
