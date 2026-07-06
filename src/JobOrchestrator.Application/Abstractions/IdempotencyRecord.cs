namespace JobOrchestrator.Application.Abstractions;

public sealed record IdempotencyRecord(string IdempotencyKey, Guid JobId, string RequestHash, DateTimeOffset CreatedAt);
