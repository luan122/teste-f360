using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.Application.Contracts;

/// <summary>Message contract signaling a job is ready for a worker to claim. Produced by the creation and scheduling paths; consumed by the worker infrastructure.</summary>
public sealed record JobQueued(Guid JobId, JobTypes Type, string Priority, string CorrelationId, int Attempt);
