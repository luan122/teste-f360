using FluentValidation;

namespace JobOrchestrator.Application.Features.Jobs;

/// <summary>Validates <see cref="CreateJobCommand"/> inputs and domain invariants before handler execution.</summary>
public sealed class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
{
    public const int MaxPayloadLength = 65536;

    public CreateJobCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty();

        RuleFor(x => x.Type)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Priority)
            .IsInEnum();

        RuleFor(x => x.Payload)
            .NotEmpty()
            .MaximumLength(MaxPayloadLength);

        RuleFor(x => x.MaxAttempts)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.CorrelationId)
            .NotEmpty();

        RuleFor(x => x.ScheduledAt)
            .Must(scheduledAt => !scheduledAt.HasValue || scheduledAt.Value > DateTimeOffset.UtcNow)
            .WithMessage("ScheduledAt must be a future timestamp.");
    }
}
