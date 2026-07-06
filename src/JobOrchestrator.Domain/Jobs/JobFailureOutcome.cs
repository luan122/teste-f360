namespace JobOrchestrator.Domain.Jobs;

public enum JobFailureOutcome
{
    Requeued,
    DeadLettered,
}
