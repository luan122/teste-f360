using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JobOrchestrator.Api.Middleware;

/// <summary>Validates the presence of the <c>Idempotency-Key</c> request header and stores it in
/// <see cref="HttpContext.Items"/> for the action to read via <see cref="HttpContextIdempotencyKeyExtensions.GetIdempotencyKey"/>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresIdempotencyKeyAttribute : ActionFilterAttribute
{
    public const string HeaderName = "Idempotency-Key";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            || string.IsNullOrWhiteSpace(values))
        {
            context.Result = new ObjectResult(
                new ProblemDetails
                {
                    Title = "Bad Request",
                    Detail = $"The '{HeaderName}' header is required.",
                    Status = StatusCodes.Status400BadRequest,
                })
            { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        context.HttpContext.Items[HeaderName] = values.ToString();
    }
}

/// <summary>Provides typed access to the idempotency key stored by <see cref="RequiresIdempotencyKeyAttribute"/>.</summary>
public static class HttpContextIdempotencyKeyExtensions
{
    /// <summary>Returns the validated <c>Idempotency-Key</c> header value.
    /// Call only from an action decorated with <see cref="RequiresIdempotencyKeyAttribute"/>.</summary>
    public static string GetIdempotencyKey(this HttpContext context) =>
        context.Items.TryGetValue(RequiresIdempotencyKeyAttribute.HeaderName, out var value)
            && value is string key
            ? key
            : throw new InvalidOperationException(
                "[RequiresIdempotencyKey] has not run for this request.");
}
