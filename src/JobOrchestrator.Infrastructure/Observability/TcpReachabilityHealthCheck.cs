using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JobOrchestrator.Infrastructure.Observability;

/// <summary>
/// A minimal <see cref="IHealthCheck"/> that reports healthy when a TCP connection to the given
/// host/port succeeds within the timeout, unhealthy otherwise. Used for the MongoDB/RabbitMQ
/// "ready" checks (see <see cref="ObservabilityServiceCollectionExtensions.AddObservability"/>)
/// so this project does not have to reference MongoDB.Driver-typed health check APIs that
/// conflict with the solution's centrally pinned MongoDB.Driver version.
/// </summary>
internal sealed class TcpReachabilityHealthCheck(
    Func<(string Host, int Port)> targetFactory,
    TimeSpan? timeout = null) : IHealthCheck
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var (host, port) = targetFactory();

        using var client = new TcpClient();
        using var timeoutCts = new CancellationTokenSource(timeout ?? DefaultTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await client.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"TCP connection to {host}:{port} succeeded.");
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                description: $"TCP connection to {host}:{port} failed.",
                exception: ex);
        }
    }
}
