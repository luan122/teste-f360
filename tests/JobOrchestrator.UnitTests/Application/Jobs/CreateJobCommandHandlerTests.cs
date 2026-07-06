using FluentAssertions;
using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Application.Features.Jobs;
using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Domain.Outbox;
using NSubstitute;

namespace JobOrchestrator.UnitTests.Application.Jobs;

/// <summary>
/// AC-002-5 / AC-002-8: an immediately-eligible job ends Queued with exactly one outbox row and
/// the command's CorrelationId flows onto the persisted job; a future-scheduled job ends
/// Scheduled with no outbox row (the releaser in 003 handles it later).
/// </summary>
public class CreateJobCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly IOutboxRepository _outboxRepository = Substitute.For<IOutboxRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IIdempotencyStore _idempotencyStore = Substitute.For<IIdempotencyStore>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly CreateJobCommandHandler _handler;

    public CreateJobCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);

        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Func<CancellationToken, Task>>()(CancellationToken.None));

        _idempotencyStore.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IdempotencyRecord?)null);
        _idempotencyStore.TryInsertAsync(Arg.Any<IdempotencyRecord>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _handler = new CreateJobCommandHandler(_jobRepository, _outboxRepository, _unitOfWork, _idempotencyStore, _clock);
    }

    private static CreateJobCommand Command(DateTimeOffset? scheduledAt, string correlationId) => new(
        IdempotencyKey: Guid.NewGuid().ToString(),
        RequestHash: "hash",
        Type: "send-email",
        Priority: Priority.Low,
        Payload: "{}",
        ScheduledAt: scheduledAt,
        MaxAttempts: 5,
        CorrelationId: correlationId);

    [Fact]
    public async Task Handle_ImmediateJob_EndsQueuedWithOneOutboxRow()
    {
        var correlationId = Guid.NewGuid().ToString();
        var command = Command(scheduledAt: null, correlationId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsIdempotencyConflict.Should().BeFalse();
        result.Job.Status.Should().Be(JobStatus.Queued);
        result.Job.CorrelationId.Should().Be(correlationId);

        await _jobRepository.Received(1).AddAsync(
            Arg.Is<Job>(j => j.Status == JobStatus.Queued), Arg.Any<CancellationToken>());
        await _outboxRepository.Received(1).AddAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ScheduledJob_EndsScheduledWithNoOutboxRow()
    {
        var correlationId = Guid.NewGuid().ToString();
        var command = Command(scheduledAt: Now.AddHours(1), correlationId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsIdempotencyConflict.Should().BeFalse();
        result.Job.Status.Should().Be(JobStatus.Scheduled);
        result.Job.CorrelationId.Should().Be(correlationId);

        await _jobRepository.Received(1).AddAsync(
            Arg.Is<Job>(j => j.Status == JobStatus.Scheduled), Arg.Any<CancellationToken>());
        await _outboxRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Handle_DuplicateIdempotencyKey_SameHash_ReturnsExistingJobWithoutPersisting()
    {
        var existingJob = Job.Create(
            Guid.NewGuid(), "key-1", "send-email", "{}", Priority.Low, null, 5,
            Guid.NewGuid().ToString(), Now);
        var record = new IdempotencyRecord("key-1", existingJob.JobId, "hash", Now);

        _idempotencyStore.FindAsync("key-1", Arg.Any<CancellationToken>()).Returns(record);
        _jobRepository.GetByIdAsync(existingJob.JobId, Arg.Any<CancellationToken>()).Returns(existingJob);

        var command = new CreateJobCommand("key-1", "hash", "send-email", Priority.Low, "{}", null, 5, "corr-1");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsIdempotencyConflict.Should().BeFalse();
        result.Job.Should().BeSameAs(existingJob);
        await _jobRepository.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateIdempotencyKey_DifferentHash_ReturnsConflict()
    {
        var existingJob = Job.Create(
            Guid.NewGuid(), "key-1", "send-email", "{}", Priority.Low, null, 5,
            Guid.NewGuid().ToString(), Now);
        var record = new IdempotencyRecord("key-1", existingJob.JobId, "hash-original", Now);

        _idempotencyStore.FindAsync("key-1", Arg.Any<CancellationToken>()).Returns(record);
        _jobRepository.GetByIdAsync(existingJob.JobId, Arg.Any<CancellationToken>()).Returns(existingJob);

        var command = new CreateJobCommand("key-1", "hash-different", "send-email", Priority.Low, "{}", null, 5, "corr-1");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsIdempotencyConflict.Should().BeTrue();
        result.Job.Should().BeSameAs(existingJob);
        await _jobRepository.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }
}
