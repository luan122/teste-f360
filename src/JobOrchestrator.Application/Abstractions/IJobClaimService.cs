namespace JobOrchestrator.Application.Abstractions;

/// <summary>Atomic distributed claim ensuring at most one worker processes a given job via a single conditional update (<c>Queued -&gt; Processing</c>) at the persistence layer.</summary>
public interface IJobClaimService
{
    /// <returns>True if this call won the claim; false if the job was already claimed, or is
    /// no longer in a claimable status (already completed, cancelled, etc.).</returns>
    Task<bool> TryClaimAsync(Guid jobId, DateTimeOffset now, CancellationToken cancellationToken);
}
