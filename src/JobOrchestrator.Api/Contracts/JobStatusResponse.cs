using JobOrchestrator.Application.Features.Jobs;
using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.Api.Contracts;

/// <summary>Mirrors <c>JobStatusResponse</c> in contracts/openapi.yaml.</summary>
public sealed record JobStatusResponse(
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
    DateTimeOffset UpdatedAt)
{
    public static JobStatusResponse FromDto(JobStatusDto dto) => new(
        dto.JobId, dto.Type, dto.Priority, dto.Status, dto.ScheduledAt, dto.Attempts,
        dto.MaxAttempts, dto.CorrelationId, dto.Error, dto.CreatedAt, dto.UpdatedAt);
}
