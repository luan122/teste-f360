using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace JobOrchestrator.Api.Middleware;

/// <summary>Adds the <c>Idempotency-Key</c> required header parameter to any Swagger operation
/// whose action method is decorated with <see cref="RequiresIdempotencyKeyAttribute"/>.</summary>
public sealed class IdempotencyKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!context.MethodInfo
                .GetCustomAttributes(typeof(RequiresIdempotencyKeyAttribute), inherit: true)
                .Any())
            return;

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = RequiresIdempotencyKeyAttribute.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Client-generated unique key ensuring at-most-once job submission.",
            Schema = new OpenApiSchema { Type = "string" },
        });
    }
}
