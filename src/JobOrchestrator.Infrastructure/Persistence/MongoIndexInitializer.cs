using JobOrchestrator.Infrastructure.Persistence.Documents;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace JobOrchestrator.Infrastructure.Persistence;

/// <summary>Creates required indexes idempotently at startup.</summary>
public sealed class MongoIndexInitializer(IMongoDatabase database) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var jobs = database.GetCollection<JobDocument>(MongoCollectionNames.Jobs);
        await jobs.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<JobDocument>(
                    Builders<JobDocument>.IndexKeys.Ascending(d => d.IdempotencyKey),
                    new CreateIndexOptions { Name = "ux_jobs_idempotencyKey", Unique = true }),
                new CreateIndexModel<JobDocument>(
                    Builders<JobDocument>.IndexKeys.Ascending(d => d.Status).Ascending(d => d.ScheduledAt),
                    new CreateIndexOptions { Name = "ix_jobs_status_scheduledAt" }),
            ],
            cancellationToken);

        var outbox = database.GetCollection<OutboxMessageDocument>(MongoCollectionNames.Outbox);
        await outbox.Indexes.CreateOneAsync(
            new CreateIndexModel<OutboxMessageDocument>(
                Builders<OutboxMessageDocument>.IndexKeys.Ascending(d => d.Status).Ascending(d => d.CreatedAt),
                new CreateIndexOptions { Name = "ix_outbox_status_createdAt" }),
            cancellationToken: cancellationToken);

        var idempotency = database.GetCollection<IdempotencyDocument>(MongoCollectionNames.Idempotency);
        await idempotency.Indexes.CreateOneAsync(
            new CreateIndexModel<IdempotencyDocument>(
                Builders<IdempotencyDocument>.IndexKeys.Ascending(d => d.CreatedAt),
                new CreateIndexOptions { Name = "ttl_idempotency_createdAt", ExpireAfter = TimeSpan.FromHours(24) }),
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
