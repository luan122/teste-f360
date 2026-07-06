using System.Text.Json;

namespace JobOrchestrator.Api.Contracts;

/// <summary>Mirrors <c>CreateJobRequest</c> in contracts/openapi.yaml.</summary>
public sealed class CreateJobRequest
{
    public string Type { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public int? MaxAttempts { get; set; }
    public JsonElement? Payload { get; set; }
}
