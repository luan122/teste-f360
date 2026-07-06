using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace JobOrchestrator.Infrastructure.Persistence.Documents;

public sealed class IdempotencyDocument
{
    [BsonId]
    public string IdempotencyKey { get; set; } = string.Empty;

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid JobId { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
