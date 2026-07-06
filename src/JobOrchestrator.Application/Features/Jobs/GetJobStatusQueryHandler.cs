using JobOrchestrator.Application.Abstractions;
using MediatR;

namespace JobOrchestrator.Application.Features.Jobs;

public sealed class GetJobStatusQueryHandler(IJobRepository jobRepository)
    : IRequestHandler<GetJobStatusQuery, JobStatusDto?>
{
    public async Task<JobStatusDto?> Handle(GetJobStatusQuery request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        return new JobStatusDto(
            job.JobId,
            job.Type,
            job.Priority.ToString(),
            job.Status.ToString(),
            job.ScheduledAt,
            job.Attempts,
            job.MaxAttempts,
            job.CorrelationId,
            job.Error,
            job.CreatedAt,
            job.UpdatedAt);
    }
}
