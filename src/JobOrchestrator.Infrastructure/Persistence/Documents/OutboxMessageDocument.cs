using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace JobOrchestrator.Infrastructure.Persistence.Documents;

public sealed class OutboxMessageDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid OutboxMessageId { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid JobId { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntil { get; set; }
}
