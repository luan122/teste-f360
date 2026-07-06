using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Infrastructure.Persistence.Documents;
using JobOrchestrator.Infrastructure.Persistence.Mappers;
using MongoDB.Driver;

namespace JobOrchestrator.Infrastructure.Persistence;

public sealed class MongoJobRepository(IMongoDatabase database, IMongoSessionAccessor sessionAccessor) : IJobRepository
{
    private readonly IMongoCollection<JobDocument> _collection = database.GetCollection<JobDocument>(MongoCollectionNames.Jobs);

    public Task AddAsync(Job job, CancellationToken cancellationToken)
    {
        var document = JobMapper.ToDocument(job);
        var session = sessionAccessor.Current;
        return session is not null
            ? _collection.InsertOneAsync(session, document, cancellationToken: cancellationToken)
            : _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task<Job?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(d => d.JobId == jobId).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : JobMapper.ToDomain(document);
    }

    public async Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(d => d.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : JobMapper.ToDomain(document);
    }

    public async Task<IReadOnlyList<Job>> GetDueScheduledJobsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var filter = Builders<JobDocument>.Filter.Eq(d => d.Status, JobStatus.Scheduled.ToString())
            & Builders<JobDocument>.Filter.Lte(d => d.ScheduledAt, now.UtcDateTime);

        var documents = await _collection.Find(filter)
            .SortBy(d => d.ScheduledAt)
            .Limit(batchSize)
            .ToListAsync(cancellationToken);

        return documents.Select(JobMapper.ToDomain).ToList();
    }

    public Task UpdateAsync(Job job, CancellationToken cancellationToken)
    {
        var document = JobMapper.ToDocument(job);
        var filter = Builders<JobDocument>.Filter.Eq(d => d.JobId, job.JobId);
        var session = sessionAccessor.Current;
        return session is not null
            ? _collection.ReplaceOneAsync(session, filter, document, cancellationToken: cancellationToken)
            : _collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
    }
}
