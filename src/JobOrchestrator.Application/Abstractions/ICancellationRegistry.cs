namespace JobOrchestrator.Application.Abstractions;

/// <summary>
/// In-process registry of cancellation tokens for jobs currently <c>Processing</c> on this worker instance.
/// The cancel endpoint calls <see cref="Cancel"/>; the worker consumer calls <see cref="Register"/> before
/// invoking a handler and <see cref="Release"/> once done.
/// </summary>
public interface ICancellationRegistry
{
    CancellationToken Register(Guid jobId, CancellationToken linkedToken);

    /// <returns>True if a token was found and signaled; false if no in-flight processing is registered for this job.</returns>
    bool Cancel(Guid jobId);

    void Release(Guid jobId);
}
