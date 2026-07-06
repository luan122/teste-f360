using FluentAssertions;
using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.UnitTests.Domain.Jobs;

public class JobPriorityComparerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    private static Job CreateJob(Priority priority, DateTimeOffset createdAt) =>
        Job.Create(Guid.NewGuid(), Guid.NewGuid().ToString(), "type", "{}", priority, null, 3,
            Guid.NewGuid().ToString(), createdAt);

    // AC-000-2 / AC-003-1: High sorts ahead of Low
    [Fact]
    public void Sort_HighAndLowJobs_HighComesFirst()
    {
        var low = CreateJob(Priority.Low, Now);
        var high = CreateJob(Priority.High, Now.AddSeconds(1));

        var ordered = new[] { low, high }.OrderBy(j => j, JobPriorityComparer.Instance).ToList();

        ordered.Should().Equal(high, low);
    }

    [Fact]
    public void Sort_SamePriority_OldestFirst()
    {
        var older = CreateJob(Priority.Low, Now);
        var newer = CreateJob(Priority.Low, Now.AddSeconds(1));

        var ordered = new[] { newer, older }.OrderBy(j => j, JobPriorityComparer.Instance).ToList();

        ordered.Should().Equal(older, newer);
    }
}
