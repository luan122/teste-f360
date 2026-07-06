namespace JobOrchestrator.Domain.Jobs;

public enum JobStatus
{
    Pending,
    Scheduled,
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled,
    DeadLettered,
}
