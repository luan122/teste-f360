using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace JobOrchestrator.Infrastructure.Persistence.Documents;

public sealed class JobDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid JobId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public BsonDocument Payload { get; set; } = [];
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public BsonDocument? Result { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
