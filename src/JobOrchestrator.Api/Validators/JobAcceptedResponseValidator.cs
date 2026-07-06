using FluentValidation;
using JobOrchestrator.Api.Contracts;

namespace JobOrchestrator.Api.Validators;

/// <summary>Validates the job-accepted response before it is sent to the caller.</summary>
public sealed class JobAcceptedResponseValidator : AbstractValidator<JobAcceptedResponse>
{
    public JobAcceptedResponseValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.CorrelationId).NotEmpty();
    }
}
