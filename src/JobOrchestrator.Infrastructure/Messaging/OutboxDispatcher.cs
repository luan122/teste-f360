using System.Text.Json;
using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Application.Contracts;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobOrchestrator.Infrastructure.Messaging;

/// <summary>
/// Polls the transactional outbox and publishes unsent records to RabbitMQ via MassTransit.
/// A publish failure for one message never stops the batch — the record stays unsent and is retried once its lease expires.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxDispatcherOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly string _leaseOwner;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxDispatcherOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _leaseOwner = $"{Environment.MachineName}-{Guid.NewGuid()}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromMilliseconds(_options.PollIntervalMilliseconds);
        var leaseDuration = TimeSpan.FromSeconds(_options.LeaseDurationSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(leaseDuration, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatcher batch failed; will retry on next poll.");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchBatchAsync(TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var leased = await outboxRepository.LeasePendingBatchAsync(
            _leaseOwner, leaseDuration, _options.BatchSize, cancellationToken);

        foreach (var message in leased)
        {
            try
            {
                if (message.MessageType != nameof(JobQueued))
                {
                    _logger.LogWarning(
                        "Skipping outbox message {OutboxMessageId} with unrecognized type {MessageType}.",
                        message.OutboxMessageId, message.MessageType);
                    continue;
                }

                var payload = JsonSerializer.Deserialize<JobQueued>(message.Payload)
                    ?? throw new InvalidOperationException("Deserialized JobQueued payload was null.");

                await publishEndpoint.Publish(
                    payload,
                    ctx => ctx.SetPriority(MapPriority(payload.Priority)),
                    cancellationToken);

                await outboxRepository.MarkSentAsync(message.OutboxMessageId, clock.UtcNow, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish outbox message {OutboxMessageId} for job {JobId}; leaving unsent for retry.",
                    message.OutboxMessageId, message.JobId);
            }
        }
    }

    private static byte MapPriority(string priority) =>
        string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase) ? (byte)9 : (byte)1;
}
