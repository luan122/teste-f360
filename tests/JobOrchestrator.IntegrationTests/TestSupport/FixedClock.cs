using JobOrchestrator.Application.Abstractions;

namespace JobOrchestrator.IntegrationTests.TestSupport;

public sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}
