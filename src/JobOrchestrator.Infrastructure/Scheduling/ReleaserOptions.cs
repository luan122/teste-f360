namespace JobOrchestrator.Infrastructure.Scheduling;

/// <summary>Configuration for <see cref="ScheduledJobReleaser"/>, bound from the "Scheduler" section.</summary>
public sealed class ReleaserOptions
{
    public const string SectionName = "Scheduler";

    public int PollIntervalSeconds { get; set; } = 1;

    public int BatchSize { get; set; } = 50;
}
