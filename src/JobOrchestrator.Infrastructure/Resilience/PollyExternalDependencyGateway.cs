using System.Net;
using System.Net.Http.Headers;
using JobOrchestrator.Application.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace JobOrchestrator.Infrastructure.Resilience;

/// <summary>
/// Guards every external-dependency call behind a Polly v8 resilience pipeline composed of a
/// circuit breaker (innermost) and a retry (outermost). This ordering ensures that an open
/// breaker rejects every retry attempt immediately without hitting the dependency.
/// </summary>
public sealed class PollyExternalDependencyGateway : IExternalDependencyGateway
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public PollyExternalDependencyGateway(HttpClient httpClient, IOptions<ResilienceOptions> options)
        : this(httpClient, BuildPipeline(options.Value))
    {
    }

    internal PollyExternalDependencyGateway(HttpClient httpClient, ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        _httpClient = httpClient;
        _pipeline = pipeline;
    }

    public async Task<string> InvokeAsync(string operation, string payload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var response = await _pipeline.ExecuteAsync(
            async ct =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, operation)
                {
                    Content = new StringContent(payload ?? string.Empty),
                };
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                return await _httpClient.SendAsync(request, ct);
            },
            cancellationToken);

        using (response)
        {
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Builds the resilience pipeline from <see cref="ResilienceOptions"/>.
    /// Exposed for unit tests that need to exercise the exact pipeline with a shorter break duration.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> BuildPipeline(
        ResilienceOptions options,
        TimeSpan? breakDurationOverride = null)
    {
        var breakDuration = breakDurationOverride ?? TimeSpan.FromSeconds(options.BreakDurationSeconds);

        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = options.FailureRatio,
            SamplingDuration = TimeSpan.FromSeconds(options.SamplingDurationSeconds),
            MinimumThroughput = options.MinimumThroughput,
            BreakDuration = breakDuration,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(IsTransientFailure),
        });

        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(200),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(IsTransientFailure),
        });

        return builder.Build();
    }

    private static bool IsTransientFailure(HttpResponseMessage response) =>
        (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout;
}
