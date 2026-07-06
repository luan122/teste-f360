using Microsoft.AspNetCore.Mvc;

namespace JobOrchestrator.Api.Contracts;

/// <summary>RFC 7807 problem responses for use outside the MVC controller pipeline.</summary>
public static class ProblemResults
{
    public static IResult ValidationProblem(IDictionary<string, string[]> errors) =>
        Results.ValidationProblem(errors, title: "One or more validation errors occurred.");

    public static IResult NotFound(string detail) =>
        Results.Problem(
            title: "Resource not found.",
            detail: detail,
            statusCode: StatusCodes.Status404NotFound);

    public static IResult Conflict(string detail) =>
        Results.Problem(
            title: "Conflict.",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);

    public static IResult BadRequest(string detail) =>
        Results.Problem(
            title: "Bad request.",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
}
