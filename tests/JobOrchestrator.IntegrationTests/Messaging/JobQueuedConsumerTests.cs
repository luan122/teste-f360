using FluentAssertions;
using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Application.Contracts;
using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Infrastructure.Jobs;
using JobOrchestrator.Infrastructure.Messaging;
using JobOrchestrator.Infrastructure.Persistence;
using JobOrchestrator.IntegrationTests.TestSupport;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace JobOrchestrator.IntegrationTests.Messaging;

/// <summary>
/// Validates <see cref="JobQueuedConsumer"/> against a real MongoDB replica set, driven through
/// MassTransit's in-memory test harness so the consumer receives a genuine
/// <c>ConsumeContext&lt;JobQueued&gt;</c> (specs/004-worker-processing, FR-004-4/5, AC-004-4):
/// redelivery of a message for a job that is no longer <c>Queued</c> (e.g. already
/// <c>Completed</c>) must be an idempotent no-op — acked without re-running the handler.
/// </summary>
public class JobQueuedConsumerTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7.0").WithReplicaSet().Build();
    private IMongoDatabase _database = null!;
    private MongoJobRepository _jobRepository = null!;
    private ServiceProvider _serviceProvider = null!;
    private ITestHarness _harness = null!;
    private CountingJobHandler _handler = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        // See MongoUnitOfWorkTests: Testcontainers remaps the host port, so the container's
        // self-advertised replica-set member address won't match unless DirectConnection is set.
        var settings = MongoClientSettings.FromConnectionString(_mongo.GetConnectionString());
        settings.DirectConnection = true;
        var client = new MongoClient(settings);
        _database = client.GetDatabase("joborchestrator_test");

        await MongoReplicaSetReadiness.WaitUntilPrimaryIsWritableAsync(_database);

        var sessionAccessor = new MongoSessionAccessor();
        _jobRepository = new MongoJobRepository(_database, sessionAccessor);
        var claimService = new MongoJobClaimService(_database);
        var clock = new FixedClock();
        _handler = new CountingJobHandler();

        var services = new ServiceCollection();
        services.AddSingleton<IJobRepository>(_jobRepository);
        services.AddSingleton<IJobClaimService>(claimService);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<ICancellationRegistry, CancellationRegistry>();
        services.AddSingleton<IJobHandler>(_handler);
        services.AddSingleton<IJobHandlerRegistry, JobHandlerRegistry>();

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<JobQueuedConsumer>();
        });

        _serviceProvider = services.BuildServiceProvider(true);
        _harness = _serviceProvider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _serviceProvider.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task Consume_RedeliveredMessageForCompletedJob_IsIdempotentNoOp()
    {
        var now = DateTimeOffset.UtcNow;
        var job = Job.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), "demo-job", "{}",
            Priority.Low, null, 3, Guid.NewGuid().ToString(), now);
        job.MarkQueued(now);
        job.StartProcessing(now);
        job.Complete("{\"ok\":true}", now);
        await _jobRepository.AddAsync(job, CancellationToken.None);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        await publishEndpoint.Publish(new JobQueued(job.JobId, job.Type, job.Priority.ToString(), job.CorrelationId, 1));

        (await _harness.Consumed.Any<JobQueued>()).Should().BeTrue();

        _handler.InvocationCount.Should().Be(0, "a redelivered message for an already-Completed job must not re-run the handler");

        var persisted = await _jobRepository.GetByIdAsync(job.JobId, CancellationToken.None);
        persisted!.Status.Should().Be(JobStatus.Completed, "the terminal state must be left untouched by the no-op consume");
    }

    [Fact]
    public async Task Consume_QueuedJob_ClaimsRunsHandlerAndCompletes()
    {
        var now = DateTimeOffset.UtcNow;
        var job = Job.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), "demo-job", "{}",
            Priority.High, null, 3, Guid.NewGuid().ToString(), now);
        job.MarkQueued(now);
        await _jobRepository.AddAsync(job, CancellationToken.None);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        await publishEndpoint.Publish(new JobQueued(job.JobId, job.Type, job.Priority.ToString(), job.CorrelationId, 1));

        (await _harness.Consumed.Any<JobQueued>()).Should().BeTrue();

        _handler.InvocationCount.Should().Be(1);

        var persisted = await _jobRepository.GetByIdAsync(job.JobId, CancellationToken.None);
        persisted!.Status.Should().Be(JobStatus.Completed);
        persisted.Result.Should().NotBeNull();
    }

    private sealed class CountingJobHandler : IJobHandler
    {
        private int _invocationCount;

        public int InvocationCount => _invocationCount;

        public string JobType => "demo-job";

        public Task<string?> HandleAsync(Job job, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _invocationCount);
            return Task.FromResult<string?>("{\"ok\":true}");
        }
    }
}
