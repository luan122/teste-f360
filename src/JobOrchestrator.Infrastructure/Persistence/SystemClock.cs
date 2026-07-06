using JobOrchestrator.Application.Abstractions;

namespace JobOrchestrator.Infrastructure.Persistence;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
