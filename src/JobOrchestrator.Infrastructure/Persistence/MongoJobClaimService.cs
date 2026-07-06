using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace JobOrchestrator.Infrastructure.Persistence;

/// <summary>Atomic <c>Queued -&gt; Processing</c> claim via a single conditional update, so two workers racing on the same job message can never both win.</summary>
public sealed class MongoJobClaimService(IMongoDatabase database) : IJobClaimService
{
    private readonly IMongoCollection<JobDocument> _collection = database.GetCollection<JobDocument>(MongoCollectionNames.Jobs);

    public async Task<bool> TryClaimAsync(Guid jobId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var filter = Builders<JobDocument>.Filter.Eq(d => d.JobId, jobId)
            & Builders<JobDocument>.Filter.Eq(d => d.Status, JobStatus.Queued.ToString());

        var update = Builders<JobDocument>.Update
            .Set(d => d.Status, JobStatus.Processing.ToString())
            .Set(d => d.UpdatedAt, now.UtcDateTime)
            .Inc(d => d.Attempts, 1);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}
