namespace JobOrchestrator.Infrastructure.Resilience;

/// <summary>Configuration for the Polly circuit-breaker and retry pipeline guarding <see cref="PollyExternalDependencyGateway"/>, bound from the "Resilience" section.</summary>
public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    /// <summary>Base address of the external dependency the gateway calls.</summary>
    public string ExternalDependencyBaseUrl { get; set; } = "https://example-external-api.local";

    /// <summary>Fraction of failed calls (within the sampling window) that trips the breaker open.</summary>
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>Rolling window over which the failure ratio is evaluated.</summary>
    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>Minimum number of calls within the sampling window before the breaker can trip.</summary>
    public int MinimumThroughput { get; set; } = 5;

    /// <summary>How long the breaker stays open before probing with a half-open trial call.</summary>
    public int BreakDurationSeconds { get; set; } = 15;
}
