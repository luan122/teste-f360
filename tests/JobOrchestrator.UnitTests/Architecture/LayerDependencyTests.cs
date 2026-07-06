using FluentAssertions;
using JobOrchestrator.Domain.Jobs;
using NetArchTest.Rules;

namespace JobOrchestrator.UnitTests.Architecture;

/// <summary>Enforces constitution.md §2.1: the Domain layer depends on nothing but the BCL.</summary>
public class LayerDependencyTests
{
    [Fact]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Job).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "MongoDB",
                "MassTransit",
                "Microsoft.AspNetCore",
                "Polly",
                "Serilog")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildFailureMessage(result));
    }

    private static string BuildFailureMessage(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Domain types violating the layer rule: " + string.Join(", ", result.FailingTypeNames ?? []);
}
