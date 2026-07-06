using JobOrchestrator.Application.Abstractions;

namespace JobOrchestrator.Infrastructure.Jobs;

/// <summary>Resolves the registered <see cref="IJobHandler"/> for a job's <c>Type</c>.</summary>
public sealed class JobHandlerRegistry : IJobHandlerRegistry
{
    private readonly Dictionary<string, IJobHandler> _handlers;

    public JobHandlerRegistry(IEnumerable<IJobHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.JobType, StringComparer.Ordinal);
    }

    public IJobHandler Resolve(string jobType)
    {
        if (_handlers.TryGetValue(jobType, out var handler))
            return handler;

        throw new PermanentJobException($"No handler registered for job type '{jobType}'.");
    }
}
