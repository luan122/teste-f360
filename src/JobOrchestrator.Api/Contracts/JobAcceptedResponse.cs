namespace JobOrchestrator.Api.Contracts;

/// <summary>Mirrors <c>JobAccepted</c> in contracts/openapi.yaml.</summary>
public sealed record JobAcceptedResponse(Guid JobId, string Status, string CorrelationId);
