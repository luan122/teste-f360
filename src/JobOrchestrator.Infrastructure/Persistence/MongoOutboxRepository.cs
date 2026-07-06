using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Domain.Outbox;
using JobOrchestrator.Infrastructure.Persistence.Documents;
using JobOrchestrator.Infrastructure.Persistence.Mappers;
using MongoDB.Driver;

namespace JobOrchestrator.Infrastructure.Persistence;

public sealed class MongoOutboxRepository(IMongoDatabase database, IMongoSessionAccessor sessionAccessor, IClock clock)
    : IOutboxRepository
{
    private readonly IMongoCollection<OutboxMessageDocument> _collection =
        database.GetCollection<OutboxMessageDocument>(MongoCollectionNames.Outbox);

    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var document = OutboxMessageMapper.ToDocument(message);
        var session = sessionAccessor.Current;
        return session is not null
            ? _collection.InsertOneAsync(session, document, cancellationToken: cancellationToken)
            : _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> LeasePendingBatchAsync(
        string leaseOwner, TimeSpan leaseDuration, int batchSize, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var leasableFilter = Builders<OutboxMessageDocument>.Filter.Eq(d => d.Status, OutboxMessageStatus.Pending.ToString())
            | (Builders<OutboxMessageDocument>.Filter.Eq(d => d.Status, OutboxMessageStatus.Dispatching.ToString())
               & Builders<OutboxMessageDocument>.Filter.Lte(d => d.LeaseUntil, now.UtcDateTime));

        var candidateIds = await _collection.Find(leasableFilter)
            .SortBy(d => d.CreatedAt)
            .Limit(batchSize)
            .Project(d => d.OutboxMessageId)
            .ToListAsync(cancellationToken);

        var leased = new List<OutboxMessage>(candidateIds.Count);
        var leaseUntil = now.Add(leaseDuration).UtcDateTime;

        foreach (var id in candidateIds)
        {
            var claimFilter = Builders<OutboxMessageDocument>.Filter.Eq(d => d.OutboxMessageId, id) & leasableFilter;
            var update = Builders<OutboxMessageDocument>.Update
                .Set(d => d.Status, OutboxMessageStatus.Dispatching.ToString())
                .Set(d => d.LeaseOwner, leaseOwner)
                .Set(d => d.LeaseUntil, leaseUntil);

            var claimed = await _collection.FindOneAndUpdateAsync(
                claimFilter, update,
                new FindOneAndUpdateOptions<OutboxMessageDocument> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (claimed is not null)
                leased.Add(OutboxMessageMapper.ToDomain(claimed));
        }

        return leased;
    }

    public Task MarkSentAsync(Guid outboxMessageId, DateTimeOffset sentAt, CancellationToken cancellationToken)
    {
        var filter = Builders<OutboxMessageDocument>.Filter.Eq(d => d.OutboxMessageId, outboxMessageId);
        var update = Builders<OutboxMessageDocument>.Update
            .Set(d => d.Status, OutboxMessageStatus.Sent.ToString())
            .Set(d => d.SentAt, sentAt.UtcDateTime)
            .Unset(d => d.LeaseOwner)
            .Unset(d => d.LeaseUntil);

        return _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
