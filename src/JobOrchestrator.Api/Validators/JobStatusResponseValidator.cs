using FluentValidation;
using JobOrchestrator.Api.Contracts;

namespace JobOrchestrator.Api.Validators;

/// <summary>Validates the job-status response before it is sent to the caller.</summary>
public sealed class JobStatusResponseValidator : AbstractValidator<JobStatusResponse>
{
    public JobStatusResponseValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Priority).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.Attempts).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxAttempts).GreaterThanOrEqualTo(1);
        RuleFor(x => x.CorrelationId).NotEmpty();
    }
}
