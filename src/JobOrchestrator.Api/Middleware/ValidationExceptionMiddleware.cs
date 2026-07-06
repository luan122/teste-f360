using FluentValidation;
using JobOrchestrator.Api.Contracts;

namespace JobOrchestrator.Api.Middleware;

/// <summary>Translates <see cref="ValidationException"/> from the MediatR pipeline into an RFC 7807 400 response.</summary>
public sealed class ValidationExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await ProblemResults.ValidationProblem(errors).ExecuteAsync(context);
        }
    }
}
