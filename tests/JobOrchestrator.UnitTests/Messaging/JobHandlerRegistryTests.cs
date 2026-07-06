using FluentAssertions;
using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Infrastructure.Jobs;

namespace JobOrchestrator.UnitTests.Messaging;

/// <summary>specs/004-worker-processing, T-004-25: handler resolution by <see cref="Job.Type"/>.</summary>
public class JobHandlerRegistryTests
{
    [Fact]
    public void Resolve_WithRegisteredType_ReturnsHandler()
    {
        var handler = new FakeJobHandler(JobTypes.Demo);
        var registry = new JobHandlerRegistry([handler]);

        var resolved = registry.Resolve(JobTypes.Demo);

        resolved.Should().BeSameAs(handler);
    }

    [Fact]
    public void Resolve_WithUnknownType_ThrowsPermanentJobException()
    {
        var registry = new JobHandlerRegistry([new FakeJobHandler(JobTypes.Demo)]);

        var act = () => registry.Resolve((JobTypes)999);

        act.Should().Throw<PermanentJobException>()
            .WithMessage("*999*");
    }

    private sealed class FakeJobHandler(JobTypes jobType) : IJobHandler
    {
        public JobTypes JobType => jobType;

        public Task<string?> HandleAsync(Job job, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }
}
