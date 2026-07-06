using FluentAssertions;
using JobOrchestrator.Domain.Exceptions;
using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.UnitTests.Domain.Jobs;

public class JobTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    private static Job CreateJob(Priority priority = Priority.Low, DateTimeOffset? scheduledAt = null, int maxAttempts = 3) =>
        Job.Create(
            jobId: Guid.NewGuid(),
            idempotencyKey: Guid.NewGuid().ToString(),
            type: JobTypes.Demo,
            payload: "{}",
            priority: priority,
            scheduledAt: scheduledAt,
            maxAttempts: maxAttempts,
            correlationId: Guid.NewGuid().ToString(),
            now: Now);

    // AC-000-3: due-check
    [Fact]
    public void Create_WithFutureScheduledAt_StartsScheduledAndIsNotDue()
    {
        var job = CreateJob(scheduledAt: Now.AddHours(1));

        job.Status.Should().Be(JobStatus.Scheduled);
        job.IsDue(Now).Should().BeFalse();
    }

    [Fact]
    public void Create_WithNoSchedule_StartsPendingAndIsDue()
    {
        var job = CreateJob(scheduledAt: null);

        job.Status.Should().Be(JobStatus.Pending);
        job.IsDue(Now).Should().BeTrue();
    }

    [Fact]
    public void IsDue_WhenScheduledAtHasArrived_ReturnsTrue()
    {
        var job = CreateJob(scheduledAt: Now.AddHours(-1));

        job.IsDue(Now).Should().BeTrue();
    }

    // AC-000-6: full legal transition walk
    [Fact]
    public void FullLifecycle_LegalTransitions_Succeed()
    {
        var job = CreateJob();

        job.MarkQueued(Now);
        job.Status.Should().Be(JobStatus.Queued);

        job.StartProcessing(Now);
        job.Status.Should().Be(JobStatus.Processing);
        job.Attempts.Should().Be(1);

        job.Complete("{\"ok\":true}", Now);
        job.Status.Should().Be(JobStatus.Completed);
        job.Result.Should().Be("{\"ok\":true}");
    }

    // AC-000-1: illegal transition from a terminal state throws and leaves state unchanged
    [Fact]
    public void StartProcessing_WhenAlreadyCompleted_ThrowsAndLeavesStateUnchanged()
    {
        var job = CreateJob();
        job.MarkQueued(Now);
        job.StartProcessing(Now);
        job.Complete(null, Now);

        var act = () => job.StartProcessing(Now);

        act.Should().Throw<InvalidJobStateTransitionException>();
        job.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public void MarkQueued_WhenAlreadyQueued_Throws()
    {
        var job = CreateJob();
        job.MarkQueued(Now);

        var act = () => job.MarkQueued(Now);

        act.Should().Throw<InvalidJobStateTransitionException>();
    }

    // AC-000-5: retry budget decides requeue vs dead-letter
    [Fact]
    public void RecordFailure_BelowMaxAttempts_Requeues()
    {
        var job = CreateJob(maxAttempts: 3);
        job.MarkQueued(Now);
        job.StartProcessing(Now); // Attempts = 1

        var outcome = job.RecordFailure("boom", Now);

        outcome.Should().Be(JobFailureOutcome.Requeued);
        job.Status.Should().Be(JobStatus.Queued);
        job.Error.Should().Be("boom");
    }

    [Fact]
    public void RecordFailure_AtMaxAttempts_DeadLetters()
    {
        var job = CreateJob(maxAttempts: 1);
        job.MarkQueued(Now);
        job.StartProcessing(Now); // Attempts = 1 == MaxAttempts

        var outcome = job.RecordFailure("boom", Now);

        outcome.Should().Be(JobFailureOutcome.DeadLettered);
        job.Status.Should().Be(JobStatus.DeadLettered);
    }

    // AC-000-4 / refined by 003 AC-003-6: cancel legality
    [Theory]
    [InlineData(JobStatus.Scheduled)]
    [InlineData(JobStatus.Queued)]
    [InlineData(JobStatus.Processing)]
    public void Cancel_FromCancellableStatus_Succeeds(JobStatus status)
    {
        var job = status == JobStatus.Scheduled
            ? CreateJob(scheduledAt: Now.AddHours(1))
            : CreateJob();

        if (status is JobStatus.Queued or JobStatus.Processing)
            job.MarkQueued(Now);
        if (status == JobStatus.Processing)
            job.StartProcessing(Now);

        job.Cancel(Now);

        job.Status.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenCompleted_Throws()
    {
        var job = CreateJob();
        job.MarkQueued(Now);
        job.StartProcessing(Now);
        job.Complete(null, Now);

        var act = () => job.Cancel(Now);

        act.Should().Throw<InvalidJobStateTransitionException>();
    }

    [Fact]
    public void Cancel_WhenDeadLettered_Throws()
    {
        var job = CreateJob(maxAttempts: 1);
        job.MarkQueued(Now);
        job.StartProcessing(Now);
        job.RecordFailure("boom", Now);
        job.Status.Should().Be(JobStatus.DeadLettered);

        var act = () => job.Cancel(Now);

        act.Should().Throw<InvalidJobStateTransitionException>();
    }

    // AC-003-6: cancelling an already-cancelled job is an idempotent no-op success
    [Fact]
    public void Cancel_WhenAlreadyCancelled_IsIdempotentNoOp()
    {
        var job = CreateJob();
        job.MarkQueued(Now);
        job.Cancel(Now);

        var act = () => job.Cancel(Now);

        act.Should().NotThrow();
        job.Status.Should().Be(JobStatus.Cancelled);
    }
}
