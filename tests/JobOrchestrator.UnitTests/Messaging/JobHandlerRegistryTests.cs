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
        var handler = new FakeJobHandler("demo-job");
        var registry = new JobHandlerRegistry([handler]);

        var resolved = registry.Resolve("demo-job");

        resolved.Should().BeSameAs(handler);
    }

    [Fact]
    public void Resolve_WithUnknownType_ThrowsPermanentJobException()
    {
        var registry = new JobHandlerRegistry([new FakeJobHandler("demo-job")]);

        var act = () => registry.Resolve("does-not-exist");

        act.Should().Throw<PermanentJobException>()
            .WithMessage("*does-not-exist*");
    }

    private sealed class FakeJobHandler(string jobType) : IJobHandler
    {
        public string JobType { get; } = jobType;

        public Task<string?> HandleAsync(Job job, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }
}
