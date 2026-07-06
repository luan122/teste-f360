using JobOrchestrator.Domain.Jobs;
using MediatR;

namespace JobOrchestrator.Application.Features.Jobs;

/// <summary>Read-path status lookup. CQRS query — never shares a handler with a command.</summary>
public sealed record GetJobStatusQuery(Guid JobId) : IRequest<JobStatusDto?>;

/// <summary>Maps 1:1 to <c>JobStatusResponse</c> in contracts/openapi.yaml.</summary>
public sealed record JobStatusDto(
    Guid JobId,
    JobTypes Type,
    string Priority,
    string Status,
    DateTimeOffset? ScheduledAt,
    int Attempts,
    int MaxAttempts,
    string CorrelationId,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
