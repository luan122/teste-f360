using FluentAssertions;
using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Application.Features.Jobs;
using JobOrchestrator.Domain.Exceptions;
using JobOrchestrator.Domain.Jobs;
using NSubstitute;

namespace JobOrchestrator.UnitTests.Application.Jobs;

/// <summary>AC-002-7: cancellation delegates legality to the domain and notifies the in-process registry.</summary>
public class CancelJobCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly IJobRepository _jobRepository = Substitute.For<IJobRepository>();
    private readonly ICancellationRegistry _cancellationRegistry = Substitute.For<ICancellationRegistry>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly CancelJobCommandHandler _handler;

    public CancelJobCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);
        _handler = new CancelJobCommandHandler(_jobRepository, _cancellationRegistry, _clock);
    }

    private static Job QueuedJob()
    {
        var job = Job.Create(Guid.NewGuid(), Guid.NewGuid().ToString(), "send-email", "{}", Priority.Low, null, 5,
            Guid.NewGuid().ToString(), Now);
        job.MarkQueued(Now);
        return job;
    }

    [Fact]
    public async Task Handle_QueuedJob_CancelsAndNotifiesRegistry()
    {
        var job = QueuedJob();
        _jobRepository.GetByIdAsync(job.JobId, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _handler.Handle(new CancelJobCommand(job.JobId), CancellationToken.None);

        result.Status.Should().Be(CancelJobStatus.Cancelled);
        job.Status.Should().Be(JobStatus.Cancelled);
        await _jobRepository.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
        _cancellationRegistry.Received(1).Cancel(job.JobId);
    }

    [Fact]
    public async Task Handle_UnknownJob_ReturnsNotFound()
    {
        var jobId = Guid.NewGuid();
        _jobRepository.GetByIdAsync(jobId, Arg.Any<CancellationToken>()).Returns((Job?)null);

        var result = await _handler.Handle(new CancelJobCommand(jobId), CancellationToken.None);

        result.Status.Should().Be(CancelJobStatus.NotFound);
        _cancellationRegistry.DidNotReceiveWithAnyArgs().Cancel(default);
    }

    [Fact]
    public async Task Handle_TerminalJob_ThrowsInvalidJobStateTransitionException()
    {
        var completedJob = Job.Create(Guid.NewGuid(), Guid.NewGuid().ToString(), "send-email", "{}", Priority.Low, null, 5,
            Guid.NewGuid().ToString(), Now);
        completedJob.MarkQueued(Now);
        completedJob.StartProcessing(Now);
        completedJob.Complete(null, Now);

        _jobRepository.GetByIdAsync(completedJob.JobId, Arg.Any<CancellationToken>()).Returns(completedJob);

        var act = async () => await _handler.Handle(new CancelJobCommand(completedJob.JobId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidJobStateTransitionException>();
        await _jobRepository.DidNotReceive().UpdateAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }
}
