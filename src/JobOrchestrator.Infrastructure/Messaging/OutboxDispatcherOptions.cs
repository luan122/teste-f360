namespace JobOrchestrator.Infrastructure.Messaging;

public sealed class OutboxDispatcherOptions
{
    public const string SectionName = "OutboxDispatcher";

    public int PollIntervalMilliseconds { get; set; } = 500;

    public int BatchSize { get; set; } = 50;

    public int LeaseDurationSeconds { get; set; } = 30;
}
