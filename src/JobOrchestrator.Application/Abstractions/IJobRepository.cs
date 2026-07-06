using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.Application.Abstractions;

public interface IJobRepository
{
    Task AddAsync(Job job, CancellationToken cancellationToken);

    Task<Job?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken);

    Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Scheduled jobs whose <see cref="Job.ScheduledAt"/> is due, oldest-due first. Used by the releaser (003).</summary>
    Task<IReadOnlyList<Job>> GetDueScheduledJobsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    /// <summary>Persists the full current state of an already-loaded job (optimistic on <c>UpdatedAt</c>).</summary>
    Task UpdateAsync(Job job, CancellationToken cancellationToken);
}
