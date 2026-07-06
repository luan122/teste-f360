using FluentAssertions;
using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Infrastructure.Persistence;
using JobOrchestrator.IntegrationTests.TestSupport;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace JobOrchestrator.IntegrationTests.Messaging;

/// <summary>Validates the atomic distributed claim (specs/004-worker-processing, FR-004-4,
/// AC-004-3) against a real MongoDB replica set: two workers racing to claim the same job must
/// never both win.</summary>
public class MongoJobClaimServiceTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7.0").WithReplicaSet().Build();
    private IMongoDatabase _database = null!;
    private MongoJobRepository _jobRepository = null!;
    private MongoJobClaimService _claimService = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        // See MongoUnitOfWorkTests: Testcontainers remaps the host port, so the container's
        // self-advertised replica-set member address won't match unless DirectConnection is set.
        var settings = MongoClientSettings.FromConnectionString(_mongo.GetConnectionString());
        settings.DirectConnection = true;
        var client = new MongoClient(settings);
        _database = client.GetDatabase("joborchestrator_test");

        var sessionAccessor = new MongoSessionAccessor();
        _jobRepository = new MongoJobRepository(_database, sessionAccessor);
        _claimService = new MongoJobClaimService(_database);

        await MongoReplicaSetReadiness.WaitUntilPrimaryIsWritableAsync(_database);
    }

    public Task DisposeAsync() => _mongo.DisposeAsync().AsTask();

    [Fact]
    public async Task TryClaimAsync_TwoConcurrentClaimsOnSameJob_ExactlyOneWins()
    {
        var now = DateTimeOffset.UtcNow;
        var job = Job.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), "demo-job", "{}",
            Priority.Low, null, 3, Guid.NewGuid().ToString(), now);
        job.MarkQueued(now);
        await _jobRepository.AddAsync(job, CancellationToken.None);

        var results = await Task.WhenAll(
            _claimService.TryClaimAsync(job.JobId, now, CancellationToken.None),
            _claimService.TryClaimAsync(job.JobId, now, CancellationToken.None));

        results.Count(won => won).Should().Be(1, "exactly one concurrent claim attempt should win the Queued -> Processing transition");

        var persisted = await _jobRepository.GetByIdAsync(job.JobId, CancellationToken.None);
        persisted!.Status.Should().Be(JobStatus.Processing);
        persisted.Attempts.Should().Be(1, "the claim increments Attempts exactly once, not once per racing caller");
    }

    [Fact]
    public async Task TryClaimAsync_JobNotQueued_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var job = Job.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), "demo-job", "{}",
            Priority.Low, null, 3, Guid.NewGuid().ToString(), now);
        // Left in Pending — never queued.
        await _jobRepository.AddAsync(job, CancellationToken.None);

        var claimed = await _claimService.TryClaimAsync(job.JobId, now, CancellationToken.None);

        claimed.Should().BeFalse();
    }
}
