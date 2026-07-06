namespace JobOrchestrator.Application.Abstractions;

/// <summary>The single boundary through which job handlers call external dependencies, wrapped in the Polly circuit-breaker and retry pipeline.</summary>
public interface IExternalDependencyGateway
{
    Task<string> InvokeAsync(string operation, string payload, CancellationToken cancellationToken);
}
