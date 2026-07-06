using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using JobOrchestrator.Api.Contracts;
using JobOrchestrator.Api.Middleware;
using JobOrchestrator.Application.Features.Jobs;
using JobOrchestrator.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobOrchestrator.Api.Controllers;

/// <summary>Handles job submission, status lookup, and cancellation.</summary>
[ApiController]
[Route("jobs")]
[Authorize]
public sealed class JobsController(
    ISender sender,
    IValidator<CreateJobRequest> createRequestValidator,
    IValidator<JobAcceptedResponse> acceptedResponseValidator,
    IValidator<JobStatusResponse> statusResponseValidator) : ControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>Submits a new job for durable processing.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateJobAsync(CancellationToken cancellationToken)
    {
        if (!HttpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var idempotencyKeyValues)
            || string.IsNullOrWhiteSpace(idempotencyKeyValues))
        {
            return Problem(
                detail: $"The '{IdempotencyKeyHeader}' header is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var idempotencyKey = idempotencyKeyValues.ToString();

        HttpContext.Request.EnableBuffering();
        using var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        HttpContext.Request.Body.Position = 0;

        CreateJobRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<CreateJobRequest>(
                rawBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return Problem(
                detail: "Request body is not valid JSON.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request is null)
        {
            return Problem(
                detail: "Request body is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var inputValidation = await createRequestValidator.ValidateAsync(request, cancellationToken);
        if (!inputValidation.IsValid)
        {
            foreach (var error in inputValidation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        var command = new CreateJobCommand(
            idempotencyKey,
            ComputeRequestHash(rawBody),
            request.Type,
            request.ParsePriority(),
            request.SerializePayload(),
            request.ScheduledAt,
            request.ResolveMaxAttempts(),
            HttpContext.GetCorrelationId());

        var result = await sender.Send(command, cancellationToken);

        if (result.IsIdempotencyConflict)
        {
            return Problem(
                detail: $"Idempotency-Key '{idempotencyKey}' was already used with a different request body.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var response = new JobAcceptedResponse(result.Job.JobId, result.Job.Status.ToString(), result.Job.CorrelationId);
        await acceptedResponseValidator.ValidateAndThrowAsync(response, cancellationToken);

        return AcceptedAtAction(nameof(GetJobAsync), new { id = result.Job.JobId }, response);
    }

    /// <summary>Returns the current status of a job.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var dto = await sender.Send(new GetJobStatusQuery(id), cancellationToken);
        if (dto is null)
        {
            return Problem(
                detail: $"Job '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var response = JobStatusResponse.FromDto(dto);
        await statusResponseValidator.ValidateAndThrowAsync(response, cancellationToken);

        return Ok(response);
    }

    /// <summary>Requests cooperative cancellation of a job.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelJobAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(new CancelJobCommand(id), cancellationToken);
            return result.Status switch
            {
                CancelJobStatus.NotFound => Problem(
                    detail: $"Job '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound),
                _ => Accepted(),
            };
        }
        catch (InvalidJobStateTransitionException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static string ComputeRequestHash(string rawBody) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
}
