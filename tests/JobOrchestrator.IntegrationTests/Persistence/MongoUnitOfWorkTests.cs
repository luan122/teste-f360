using FluentAssertions;
using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Domain.Outbox;
using JobOrchestrator.Infrastructure.Persistence;
using JobOrchestrator.Infrastructure.Persistence.Documents;
using JobOrchestrator.IntegrationTests.TestSupport;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace JobOrchestrator.IntegrationTests.Persistence;

/// <summary>Validates the transactional Outbox write (specs/004-worker-processing, AC-004-1)
/// against a real MongoDB replica set — the constitution's Outbox doctrine is only real if the
/// commit/abort semantics actually hold, not just if the code compiles.</summary>
public class MongoUnitOfWorkTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7.0").WithReplicaSet().Build();
    private IMongoDatabase _database = null!;
    private MongoJobRepository _jobRepository = null!;
    private MongoOutboxRepository _outboxRepository = null!;
    private MongoUnitOfWork _unitOfWork = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        // The container advertises its replica-set member at the internal container port, which
        // does not match Testcontainers' remapped host port. Direct connection skips topology
        // discovery of that (unreachable) advertised address; a single-node replica set still
        // supports transactions over a direct connection.
        var settings = MongoClientSettings.FromConnectionString(_mongo.GetConnectionString());
        settings.DirectConnection = true;
        var client = new MongoClient(settings);
        _database = client.GetDatabase("joborchestrator_test");
        var sessionAccessor = new MongoSessionAccessor();
        _jobRepository = new MongoJobRepository(_database, sessionAccessor);
        _outboxRepository = new MongoOutboxRepository(_database, sessionAccessor, new FixedClock());
        _unitOfWork = new MongoUnitOfWork(client, sessionAccessor);
    }

    public Task DisposeAsync() => _mongo.DisposeAsync().AsTask();

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsJobAndOutboxAtomically()
    {
        var (job, outboxMessage) = CreateJobWithOutboxMessage();

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _jobRepository.AddAsync(job, ct);
            await _outboxRepository.AddAsync(outboxMessage, ct);
        }, CancellationToken.None);

        var persistedJob = await _jobRepository.GetByIdAsync(job.JobId, CancellationToken.None);
        persistedJob.Should().NotBeNull();
        persistedJob!.Status.Should().Be(JobStatus.Pending);

        var outboxCount = await _database.GetCollection<OutboxMessageDocument>(MongoCollectionNames.Outbox)
            .CountDocumentsAsync(d => d.JobId == job.JobId);
        outboxCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenActionThrows_RollsBackBothWrites()
    {
        var (job, outboxMessage) = CreateJobWithOutboxMessage();

        var act = async () => await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _jobRepository.AddAsync(job, ct);
            await _outboxRepository.AddAsync(outboxMessage, ct);
            throw new InvalidOperationException("forced abort");
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        var persistedJob = await _jobRepository.GetByIdAsync(job.JobId, CancellationToken.None);
        persistedJob.Should().BeNull();

        var outboxCount = await _database.GetCollection<OutboxMessageDocument>(MongoCollectionNames.Outbox)
            .CountDocumentsAsync(d => d.JobId == job.JobId);
        outboxCount.Should().Be(0);
    }

    private static (Job Job, OutboxMessage OutboxMessage) CreateJobWithOutboxMessage()
    {
        var now = DateTimeOffset.UtcNow;
        var job = Job.Create(Guid.NewGuid(), Guid.NewGuid().ToString(), "send-email", "{\"to\":\"a@b.com\"}",
            Priority.Low, null, 3, Guid.NewGuid().ToString(), now);
        var outboxMessage = OutboxMessage.Create(job.JobId, "JobQueued", "{}", now);
        return (job, outboxMessage);
    }
}
