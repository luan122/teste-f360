using JobOrchestrator.Application.Abstractions;
using MediatR;

namespace JobOrchestrator.Application.Features.Jobs;

/// <summary>
/// Handles job cancellation. State-transition legality is enforced by the domain;
/// an <see cref="JobOrchestrator.Domain.Exceptions.InvalidJobStateTransitionException"/> propagates
/// to the caller for mapping to an appropriate HTTP response.
/// </summary>
public sealed class CancelJobCommandHandler(
    IJobRepository jobRepository,
    ICancellationRegistry cancellationRegistry,
    IClock clock) : IRequestHandler<CancelJobCommand, CancelJobResult>
{
    public async Task<CancelJobResult> Handle(CancelJobCommand request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (job is null)
        {
            return CancelJobResult.NotFound;
        }

        job.Cancel(clock.UtcNow);

        await jobRepository.UpdateAsync(job, cancellationToken);

        cancellationRegistry.Cancel(job.JobId);

        return CancelJobResult.Cancelled;
    }
}
