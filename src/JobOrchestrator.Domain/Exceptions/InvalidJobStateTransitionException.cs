using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.Domain.Exceptions;

public sealed class InvalidJobStateTransitionException : DomainException
{
    public InvalidJobStateTransitionException(Guid jobId, JobStatus currentStatus, string attemptedAction)
        : base($"Job {jobId} cannot perform '{attemptedAction}' while in status '{currentStatus}'.")
    {
        JobId = jobId;
        CurrentStatus = currentStatus;
        AttemptedAction = attemptedAction;
    }

    public Guid JobId { get; }
    public JobStatus CurrentStatus { get; }
    public string AttemptedAction { get; }
}
