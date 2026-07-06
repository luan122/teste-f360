using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.Application.Abstractions;

/// <summary>The pluggable unit of actual work for one <see cref="Job.Type"/>.</summary>
public interface IJobHandler
{
    JobTypes JobType { get; }

    /// <returns>An optional JSON result string stored on <see cref="Job.Result"/>.</returns>
    Task<string?> HandleAsync(Job job, CancellationToken cancellationToken);
}

public interface IJobHandlerRegistry
{
    /// <exception cref="PermanentJobException">No handler is registered for the job's type — not retryable.</exception>
    IJobHandler Resolve(JobTypes jobType);
}
