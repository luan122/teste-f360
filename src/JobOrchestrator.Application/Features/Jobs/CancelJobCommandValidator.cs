using FluentValidation;

namespace JobOrchestrator.Application.Features.Jobs;

public sealed class CancelJobCommandValidator : AbstractValidator<CancelJobCommand>
{
    public CancelJobCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
    }
}
