using System.Net;
using FluentAssertions;
using JobOrchestrator.Infrastructure.Resilience;
using Polly;
using Polly.CircuitBreaker;

namespace JobOrchestrator.UnitTests.Resilience;

/// <summary>
/// Covers the Polly circuit-breaker + retry pipeline behind
/// <see cref="PollyExternalDependencyGateway"/> (specs/005-resilience, FR-005-4/5, AC-005-4).
/// </summary>
/// <remarks>
/// Rather than going through the DI-registered HttpClient, these tests build a standalone
/// <see cref="ResiliencePipeline{T}"/> via <see cref="PollyExternalDependencyGateway.BuildPipeline"/>
/// (the same pipeline-building logic the production gateway uses, exposed publicly for
/// testability) and drive it with a fake delegate returning canned
/// <see cref="HttpResponseMessage"/>s.
/// </remarks>
public class PollyExternalDependencyGatewayTests
{
    private static ResiliencePipeline<HttpResponseMessage> BuildPipeline(
        double failureRatio = 0.5,
        int samplingDurationSeconds = 30,
        int minimumThroughput = 5,
        TimeSpan? breakDuration = null)
    {
        var options = new ResilienceOptions
        {
            FailureRatio = failureRatio,
            SamplingDurationSeconds = samplingDurationSeconds,
            MinimumThroughput = minimumThroughput,
            BreakDurationSeconds = 15,
        };

        return PollyExternalDependencyGateway.BuildPipeline(options, breakDuration);
    }

    private static Func<HttpResponseMessage> FailingResponder() =>
        () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

    private static async Task<HttpResponseMessage> ExecuteOnceAsync(
        ResiliencePipeline<HttpResponseMessage> pipeline,
        Action onInvoked,
        Func<HttpResponseMessage> responder) =>
        await pipeline.ExecuteAsync(async ct =>
        {
            onInvoked();
            await Task.Yield();
            return responder();
        });

    [Fact]
    public async Task ExecuteAsync_WhenDelegateFailsTwiceThenSucceeds_RetriesAndReturnsSuccess()
    {
        var pipeline = BuildPipeline();
        var callCount = 0;

        var response = await pipeline.ExecuteAsync(async ct =>
        {
            callCount++;
            await Task.Yield();
            return callCount <= 2
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDelegateAlwaysFails_BreakerOpensAndFailsFastWithoutInvokingDelegate()
    {
        // Small minimum throughput so the breaker trips after only a couple of logical calls
        // (each of which itself makes several attempts via the retry strategy).
        var pipeline = BuildPipeline(failureRatio: 0.5, minimumThroughput: 2);
        var callCount = 0;

        // Drive logical calls against the always-failing delegate until the breaker opens
        // (surfaced as BrokenCircuitException instead of the delegate's 503 response).
        BrokenCircuitException? brokenCircuit = null;
        for (var i = 0; i < 10 && brokenCircuit is null; i++)
        {
            try
            {
                await ExecuteOnceAsync(pipeline, () => callCount++, FailingResponder());
            }
            catch (BrokenCircuitException ex)
            {
                brokenCircuit = ex;
            }
        }

        brokenCircuit.Should().NotBeNull("the breaker must open after enough failures");

        var callCountAfterOpen = callCount;

        // Once open, a further call must fail fast WITHOUT invoking the delegate again.
        Func<Task> callWhileOpen = async () =>
            await ExecuteOnceAsync(pipeline, () => callCount++, FailingResponder());

        await callWhileOpen.Should().ThrowAsync<BrokenCircuitException>();

        callCount.Should().Be(callCountAfterOpen, "an open breaker must fail fast without invoking the dependency (AC-005-4)");
    }

    [Fact]
    public async Task ExecuteAsync_AfterBreakDurationElapses_HalfOpensAndClosesOnSuccess()
    {
        // Polly enforces a minimum BreakDuration of 500ms; keep it at that floor so the test
        // still runs fast without a real-world-sized wait.
        var breakDuration = TimeSpan.FromMilliseconds(500);
        var pipeline = BuildPipeline(failureRatio: 0.5, minimumThroughput: 2, breakDuration: breakDuration);
        var callCount = 0;

        BrokenCircuitException? brokenCircuit = null;
        for (var i = 0; i < 10 && brokenCircuit is null; i++)
        {
            try
            {
                await ExecuteOnceAsync(pipeline, () => callCount++, FailingResponder());
            }
            catch (BrokenCircuitException ex)
            {
                brokenCircuit = ex;
            }
        }

        brokenCircuit.Should().NotBeNull("the breaker must open after enough failures");

        // Confirm it's open: an immediate call fails fast without invoking the delegate.
        var callsBeforeOpenCheck = callCount;
        Func<Task> openCheckCall = async () =>
            await ExecuteOnceAsync(pipeline, () => callCount++, () => new HttpResponseMessage(HttpStatusCode.OK));

        await openCheckCall.Should().ThrowAsync<BrokenCircuitException>();
        callCount.Should().Be(callsBeforeOpenCheck);

        // Wait past the break duration so the breaker transitions to half-open on the next call.
        await Task.Delay(breakDuration + TimeSpan.FromMilliseconds(200));

        var response = await ExecuteOnceAsync(pipeline, () => callCount++, () => new HttpResponseMessage(HttpStatusCode.OK));
        response.StatusCode.Should().Be(HttpStatusCode.OK, "a successful half-open trial call closes the breaker");

        // Breaker should now be closed: a further call succeeds without being rejected.
        var closedResponse = await ExecuteOnceAsync(pipeline, () => callCount++, () => new HttpResponseMessage(HttpStatusCode.OK));
        closedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
