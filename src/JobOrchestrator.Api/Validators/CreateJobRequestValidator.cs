using FluentValidation;
using JobOrchestrator.Api.Contracts;
using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.Api.Validators;

/// <summary>Validates the HTTP request body for job creation before command dispatch.</summary>
public sealed class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotNull()
            .IsInEnum();

        RuleFor(x => x.Priority)
            .Must(p => p is null || Enum.TryParse<Priority>(p, ignoreCase: false, out _))
            .WithMessage($"Priority must be one of: {string.Join(", ", Enum.GetNames<Priority>())}.");

        RuleFor(x => x.MaxAttempts)
            .InclusiveBetween(1, 100)
            .When(x => x.MaxAttempts.HasValue)
            .WithMessage("MaxAttempts must be between 1 and 100.");

        RuleFor(x => x.Payload)
            .NotNull()
            .WithMessage("Payload is required.");

        RuleFor(x => x.ScheduledAt)
            .Must(scheduledAt => !scheduledAt.HasValue || scheduledAt.Value > DateTimeOffset.UtcNow)
            .WithMessage("ScheduledAt must be a future timestamp if provided.");
    }
}
