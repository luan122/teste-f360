using FluentValidation;

namespace JobOrchestrator.Application.Features.Jobs;

/// <summary>Validates the job-status query before handler execution.</summary>
public sealed class GetJobStatusQueryValidator : AbstractValidator<GetJobStatusQuery>
{
    public GetJobStatusQueryValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
    }
}
