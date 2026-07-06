using JobOrchestrator.Application.Abstractions;

using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.Infrastructure.Jobs;

/// <summary>Resolves the registered <see cref="IJobHandler"/> for a job's <c>Type</c>.</summary>
public sealed class JobHandlerRegistry : IJobHandlerRegistry
{
    private readonly Dictionary<JobTypes, IJobHandler> _handlers;

    public JobHandlerRegistry(IEnumerable<IJobHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.JobType);
    }

    public IJobHandler Resolve(JobTypes jobType)
    {
        if (_handlers.TryGetValue(jobType, out var handler))
            return handler;

        throw new PermanentJobException($"No handler registered for job type '{jobType}'.");
    }
}
