using MediatR;

namespace JobOrchestrator.Application.Features.Jobs;

/// <summary>Requests cooperative cancellation of a job.</summary>
public sealed record CancelJobCommand(Guid JobId) : IRequest<CancelJobResult>;

/// <summary>Possible outcomes of <see cref="CancelJobCommand"/>.</summary>
public enum CancelJobStatus
{
    Cancelled,
    NotFound,
}

/// <summary>Outcome of <see cref="CancelJobCommand"/>.</summary>
public sealed record CancelJobResult(CancelJobStatus Status)
{
    public static CancelJobResult Cancelled { get; } = new(CancelJobStatus.Cancelled);

    public static CancelJobResult NotFound { get; } = new(CancelJobStatus.NotFound);
}
