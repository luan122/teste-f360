namespace JobOrchestrator.Domain.Outbox;

/// <summary>Durable record of intent to publish, written in the same transaction as its associated job. A separate dispatcher publishes and marks it sent.</summary>
public sealed class OutboxMessage
{
    private OutboxMessage(Guid outboxMessageId, Guid jobId, string messageType, string payload, DateTimeOffset createdAt)
    {
        OutboxMessageId = outboxMessageId;
        JobId = jobId;
        MessageType = messageType;
        Payload = payload;
        Status = OutboxMessageStatus.Pending;
        CreatedAt = createdAt;
    }

    public Guid OutboxMessageId { get; }
    public Guid JobId { get; }
    public string MessageType { get; }
    public string Payload { get; }
    public OutboxMessageStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }

    public static OutboxMessage Create(Guid jobId, string messageType, string payload, DateTimeOffset now) =>
        new(Guid.NewGuid(), jobId, messageType, payload, now);

    public static OutboxMessage Rehydrate(
        Guid outboxMessageId, Guid jobId, string messageType, string payload, OutboxMessageStatus status,
        DateTimeOffset createdAt, DateTimeOffset? sentAt, string? leaseOwner, DateTimeOffset? leaseUntil)
    {
        var message = new OutboxMessage(outboxMessageId, jobId, messageType, payload, createdAt)
        {
            Status = status,
            SentAt = sentAt,
            LeaseOwner = leaseOwner,
            LeaseUntil = leaseUntil,
        };
        return message;
    }

    /// <summary>True once a dispatcher can safely attempt this row: never leased, or a stale lease expired.</summary>
    public bool IsLeasable(DateTimeOffset now) =>
        Status == OutboxMessageStatus.Pending || (Status == OutboxMessageStatus.Dispatching && LeaseUntil is { } until && until <= now);

    /// <summary>Reflects a lease already won atomically at the persistence layer onto this in-memory instance.</summary>
    public void Lease(string owner, DateTimeOffset now, TimeSpan leaseDuration)
    {
        Status = OutboxMessageStatus.Dispatching;
        LeaseOwner = owner;
        LeaseUntil = now + leaseDuration;
    }

    public void MarkSent(DateTimeOffset now)
    {
        Status = OutboxMessageStatus.Sent;
        SentAt = now;
        LeaseOwner = null;
        LeaseUntil = null;
    }
}
