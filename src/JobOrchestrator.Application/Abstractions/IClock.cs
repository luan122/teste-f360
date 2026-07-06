namespace JobOrchestrator.Application.Abstractions;

/// <summary>Testable source of "now". Implemented by a system clock in Infrastructure.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
