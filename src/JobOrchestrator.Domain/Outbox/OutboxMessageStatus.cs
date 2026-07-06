namespace JobOrchestrator.Domain.Outbox;

public enum OutboxMessageStatus
{
    Pending,
    Dispatching,
    Sent,
}
