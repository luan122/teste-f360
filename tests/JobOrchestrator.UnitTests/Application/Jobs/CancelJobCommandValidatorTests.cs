using FluentValidation.TestHelper;
using JobOrchestrator.Application.Features.Jobs;

namespace JobOrchestrator.UnitTests.Application.Jobs;

public class CancelJobCommandValidatorTests
{
    private readonly CancelJobCommandValidator _validator = new();

    [Fact]
    public void Valid_JobId_PassesValidation()
    {
        var result = _validator.TestValidate(new CancelJobCommand(Guid.NewGuid()));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_JobId_FailsValidation()
    {
        var result = _validator.TestValidate(new CancelJobCommand(Guid.Empty));

        result.ShouldHaveValidationErrorFor(x => x.JobId);
    }
}
