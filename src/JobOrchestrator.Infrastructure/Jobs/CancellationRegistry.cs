using System.Collections.Concurrent;
using JobOrchestrator.Application.Abstractions;

namespace JobOrchestrator.Infrastructure.Jobs;

public sealed class CancellationRegistry : ICancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public CancellationToken Register(Guid jobId, CancellationToken linkedToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(linkedToken);
        _sources[jobId] = source;
        return source.Token;
    }

    public bool Cancel(Guid jobId)
    {
        if (!_sources.TryGetValue(jobId, out var source))
            return false;

        source.Cancel();
        return true;
    }

    public void Release(Guid jobId)
    {
        if (_sources.TryRemove(jobId, out var source))
            source.Dispose();
    }
}
