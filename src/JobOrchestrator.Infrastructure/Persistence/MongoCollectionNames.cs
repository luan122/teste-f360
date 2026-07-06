namespace JobOrchestrator.Infrastructure.Persistence;

public static class MongoCollectionNames
{
    public const string Jobs = "jobs";
    public const string Outbox = "outbox";
    public const string Idempotency = "idempotency";
}
