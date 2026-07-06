using FluentAssertions;
using FluentValidation.TestHelper;
using JobOrchestrator.Application.Features.Jobs;
using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.UnitTests.Application.Jobs;

/// <summary>AC-002-4: strict validation of CreateJobCommand.</summary>
public class CreateJobCommandValidatorTests
{
    private readonly CreateJobCommandValidator _validator = new();

    private static CreateJobCommand ValidCommand() => new(
        IdempotencyKey: Guid.NewGuid().ToString(),
        RequestHash: "hash",
        Type: JobTypes.Demo,
        Priority: Priority.Low,
        Payload: "{}",
        ScheduledAt: null,
        MaxAttempts: 5,
        CorrelationId: Guid.NewGuid().ToString());

    [Fact]
    public void Valid_Command_PassesValidation()
    {
        var result = _validator.TestValidate(ValidCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Missing_Type_FailsValidation()
    {
        var command = ValidCommand() with { Type = (JobTypes)999 };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Type);
    }



    [Fact]
    public void Invalid_Priority_FailsValidation()
    {
        var command = ValidCommand() with { Priority = (Priority)99 };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Priority);
    }

    [Fact]
    public void Missing_Payload_FailsValidation()
    {
        var command = ValidCommand() with { Payload = string.Empty };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Payload);
    }

    [Fact]
    public void Oversized_Payload_FailsValidation()
    {
        var command = ValidCommand() with { Payload = new string('a', CreateJobCommandValidator.MaxPayloadLength + 1) };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Payload);
    }

    [Fact]
    public void MaxAttempts_LessThanOne_FailsValidation()
    {
        var command = ValidCommand() with { MaxAttempts = 0 };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MaxAttempts);
    }

    [Fact]
    public void Missing_IdempotencyKey_FailsValidation()
    {
        var command = ValidCommand() with { IdempotencyKey = string.Empty };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }

    [Fact]
    public void Missing_CorrelationId_FailsValidation()
    {
        var command = ValidCommand() with { CorrelationId = string.Empty };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CorrelationId);
    }
}
