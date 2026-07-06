using FluentAssertions;
using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Infrastructure.Persistence;
using JobOrchestrator.Infrastructure.Persistence.Documents;
using JobOrchestrator.Infrastructure.Scheduling;
using JobOrchestrator.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace JobOrchestrator.IntegrationTests.Scheduling;

/// <summary>Validates the scheduled-job releaser (specs/003-task-management, FR-003-3/FR-003-4,
/// AC-003-2/AC-003-3) against a real MongoDB replica set: due jobs are released to Queued with an
/// outbox row, and not-yet-due jobs are left alone.</summary>
public class ScheduledJobReleaserTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7.0").WithReplicaSet().Build();
    private IMongoDatabase _database = null!;
    private MongoJobRepository _jobRepository = null!;
    private ServiceProvider _serviceProvider = null!;
    private ScheduledJobReleaser _releaser = null!;
    private CancellationTokenSource _releaserCts = null!;
    private Task _releaserTask = null!;

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
        var clock = new FixedClock();

        _jobRepository = new MongoJobRepository(_database, sessionAccessor);

        var services = new ServiceCollection();
        services.AddSingleton<IMongoClient>(client);
        services.AddSingleton(_database);
        services.AddSingleton(sessionAccessor);
        services.AddSingleton<IMongoSessionAccessor>(sessionAccessor);
        services.AddSingleton<IClock>(clock);
        services.AddScoped<IUnitOfWork, MongoUnitOfWork>();
        services.AddScoped<IJobRepository>(_ => _jobRepository);
        services.AddScoped<IOutboxRepository, MongoOutboxRepository>();
        services.Configure<ReleaserOptions>(o =>
        {
            o.PollIntervalSeconds = 1;
            o.BatchSize = 50;
        });

        _serviceProvider = services.BuildServiceProvider();
        Clock = clock;

        _releaser = new ScheduledJobReleaser(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            clock,
            _serviceProvider.GetRequiredService<IOptions<ReleaserOptions>>(),
            new TestConsoleLogger<ScheduledJobReleaser>());

        // The replica set reports itself "ready" (per Testcontainers' readiness check) slightly
        // before the driver can reliably route a write to the elected primary; a write issued
        // immediately after can fail with MongoNotPrimaryException. A short settle + retry
        // absorbs that without weakening what the test actually verifies.
        await WaitUntilPrimaryIsWritableAsync(_database, CancellationToken.None);

        _releaserCts = new CancellationTokenSource();
        _releaserTask = _releaser.StartAsync(_releaserCts.Token);
    }

    private FixedClock Clock { get; set; } = null!;

    public async Task DisposeAsync()
    {
        _releaserCts.Cancel();
        try
        {
            await _releaser.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // expected on cancellation
        }

        _releaserCts.Dispose();
        await _serviceProvider.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task Releaser_ReleasesJobWhenScheduledAtIsInThePast()
    {
        var now = Clock.UtcNow;
        var dueJob = Job.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), "send-email", "{\"to\":\"a@b.com\"}",
            Priority.Low, now.AddMinutes(5), 3, Guid.NewGuid().ToString(), now);
        await _jobRepository.AddAsync(dueJob, CancellationToken.None);

        // Move the clock past ScheduledAt so the job becomes due on the next poll.
        Clock.UtcNow = now.AddMinutes(6);

        var released = await WaitForConditionAsync(async () =>
        {
            var job = await _jobRepository.GetByIdAsync(dueJob.JobId, CancellationToken.None);
            return job is not null && job.Status == JobStatus.Queued;
        }, TimeSpan.FromSeconds(10));

        released.Should().BeTrue("the due job should be released to Queued within a couple of poll intervals");

        var persisted = await _jobRepository.GetByIdAsync(dueJob.JobId, CancellationToken.None);
        persisted!.Status.Should().Be(JobStatus.Queued);

        var outboxCollection = _database.GetCollection<OutboxMessageDocument>(MongoCollectionNames.Outbox);
        var outboxMessage = await outboxCollection.Find(d => d.JobId == dueJob.JobId).FirstOrDefaultAsync();
        outboxMessage.Should().NotBeNull();
        outboxMessage!.MessageType.Should().Be("JobQueued");
        outboxMessage.Payload.Should().Contain(dueJob.JobId.ToString());
    }

    [Fact]
    public async Task Releaser_DoesNotReleaseJobWhenScheduledAtIsInTheFuture()
    {
        var now = Clock.UtcNow;
        var futureJob = Job.Create(
            Guid.NewGuid(), Guid.NewGuid().ToString(), "send-email", "{\"to\":\"a@b.com\"}",
            Priority.Low, now.AddHours(1), 3, Guid.NewGuid().ToString(), now);
        await _jobRepository.AddAsync(futureJob, CancellationToken.None);

        // Give the releaser a few poll intervals' worth of time to (incorrectly) act.
        await Task.Delay(TimeSpan.FromSeconds(3));

        var persisted = await _jobRepository.GetByIdAsync(futureJob.JobId, CancellationToken.None);
        persisted!.Status.Should().Be(JobStatus.Scheduled);

        var outboxCollection = _database.GetCollection<OutboxMessageDocument>(MongoCollectionNames.Outbox);
        var outboxCount = await outboxCollection.CountDocumentsAsync(d => d.JobId == futureJob.JobId);
        outboxCount.Should().Be(0);
    }

    private static async Task<bool> WaitForConditionAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            if (await condition())
                return true;

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return await condition();
    }

    private static async Task WaitUntilPrimaryIsWritableAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var probe = database.GetCollection<OutboxMessageDocument>("__readiness_probe");
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (true)
        {
            try
            {
                await probe.EstimatedDocumentCountAsync(cancellationToken: cancellationToken);
                var doc = OutboxMessageDocument_ForProbe();
                await probe.InsertOneAsync(doc, cancellationToken: cancellationToken);
                await probe.DeleteOneAsync(d => d.OutboxMessageId == doc.OutboxMessageId, cancellationToken);
                return;
            }
            catch (MongoException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
        }
    }

    private static OutboxMessageDocument OutboxMessageDocument_ForProbe() => new()
    {
        OutboxMessageId = Guid.NewGuid(),
        JobId = Guid.NewGuid(),
        MessageType = "probe",
        Payload = "{}",
        Status = "Pending",
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>Minimal <see cref="ILogger{T}"/> that writes straight to the test console so
    /// poll-failure diagnostics surface during local debugging.</summary>
    private sealed class TestConsoleLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{logLevel}] {formatter(state, exception)}{(exception is null ? string.Empty : $"\n{exception}")}");
        }
    }
}
