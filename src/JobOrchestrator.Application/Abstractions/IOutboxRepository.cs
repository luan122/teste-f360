using JobOrchestrator.Domain.Outbox;

namespace JobOrchestrator.Application.Abstractions;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically leases up to <paramref name="batchSize"/> leasable rows (see
    /// <see cref="OutboxMessage.IsLeasable"/>) to <paramref name="leaseOwner"/> and returns them
    /// already reflecting the lease. Implementations MUST perform the claim as a single atomic
    /// per-document operation so two dispatcher instances never win the same row.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> LeasePendingBatchAsync(
        string leaseOwner, TimeSpan leaseDuration, int batchSize, CancellationToken cancellationToken);

    Task MarkSentAsync(Guid outboxMessageId, DateTimeOffset sentAt, CancellationToken cancellationToken);
}
