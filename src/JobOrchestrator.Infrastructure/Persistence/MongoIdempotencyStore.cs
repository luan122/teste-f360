using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace JobOrchestrator.Infrastructure.Persistence;

public sealed class MongoIdempotencyStore(IMongoDatabase database) : IIdempotencyStore
{
    private readonly IMongoCollection<IdempotencyDocument> _collection =
        database.GetCollection<IdempotencyDocument>(MongoCollectionNames.Idempotency);

    public async Task<IdempotencyRecord?> FindAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(d => d.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
        return document is null
            ? null
            : new IdempotencyRecord(document.IdempotencyKey, document.JobId, document.RequestHash, new DateTimeOffset(document.CreatedAt, TimeSpan.Zero));
    }

    public async Task<bool> TryInsertAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        var document = new IdempotencyDocument
        {
            IdempotencyKey = record.IdempotencyKey,
            JobId = record.JobId,
            RequestHash = record.RequestHash,
            CreatedAt = record.CreatedAt.UtcDateTime,
        };

        try
        {
            await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
