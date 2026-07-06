using System.Text.Json;

using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.Api.Contracts;

/// <summary>Mirrors <c>CreateJobRequest</c> in contracts/openapi.yaml.</summary>
public sealed class CreateJobRequest
{
    public JobTypes? Type { get; set; }
    public string? Priority { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public int? MaxAttempts { get; set; }
    public JsonElement? Payload { get; set; }
}
