using JobOrchestrator.Domain.Outbox;
using JobOrchestrator.Infrastructure.Persistence.Documents;

namespace JobOrchestrator.Infrastructure.Persistence.Mappers;

public static class OutboxMessageMapper
{
    public static OutboxMessageDocument ToDocument(OutboxMessage message) => new()
    {
        OutboxMessageId = message.OutboxMessageId,
        JobId = message.JobId,
        MessageType = message.MessageType,
        Payload = message.Payload,
        Status = message.Status.ToString(),
        CreatedAt = message.CreatedAt.UtcDateTime,
        SentAt = message.SentAt?.UtcDateTime,
        LeaseOwner = message.LeaseOwner,
        LeaseUntil = message.LeaseUntil?.UtcDateTime,
    };

    public static OutboxMessage ToDomain(OutboxMessageDocument document) => OutboxMessage.Rehydrate(
        outboxMessageId: document.OutboxMessageId,
        jobId: document.JobId,
        messageType: document.MessageType,
        payload: document.Payload,
        status: Enum.Parse<OutboxMessageStatus>(document.Status),
        createdAt: new DateTimeOffset(document.CreatedAt, TimeSpan.Zero),
        sentAt: document.SentAt is { } sentAt ? new DateTimeOffset(sentAt, TimeSpan.Zero) : null,
        leaseOwner: document.LeaseOwner,
        leaseUntil: document.LeaseUntil is { } leaseUntil ? new DateTimeOffset(leaseUntil, TimeSpan.Zero) : null);
}
