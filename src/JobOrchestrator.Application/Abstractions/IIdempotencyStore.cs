namespace JobOrchestrator.Application.Abstractions;

/// <summary>Guarantees at-most-one job per <c>Idempotency-Key</c> via a unique index, so concurrent identical requests race safely.</summary>
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// <returns>True if this call created the record; false if the key already existed (caller
    /// should re-<see cref="FindAsync"/> to compare <see cref="IdempotencyRecord.RequestHash"/>).</returns>
    Task<bool> TryInsertAsync(IdempotencyRecord record, CancellationToken cancellationToken);
}
