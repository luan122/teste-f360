using Serilog.Context;

namespace JobOrchestrator.Api.Middleware;

/// <summary>
/// Reads or generates the <c>X-Correlation-Id</c> for each request, pushes it into Serilog's
/// <see cref="LogContext"/>, and echoes it on the response.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var provided) && !string.IsNullOrWhiteSpace(provided)
            ? provided.ToString()
            : Guid.NewGuid().ToString();

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

/// <summary>Provides access to the current request's correlation identifier from <see cref="HttpContext"/>.</summary>
public static class HttpContextCorrelationIdExtensions
{
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value) && value is string correlationId
            ? correlationId
            : throw new InvalidOperationException("CorrelationIdMiddleware has not run for this request.");
}
