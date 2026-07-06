namespace JobOrchestrator.Application.Abstractions;

/// <summary>Transaction boundary for the Outbox pattern: a job and its outbox message must be written atomically via a MongoDB session transaction.</summary>
public interface IUnitOfWork
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
