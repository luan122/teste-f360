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
    /// <summary>Submits a new job for durable processing.</summary>
    [HttpPost]
    [RequiresIdempotencyKey]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(JobAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateJobAsync(
        [FromBody] CreateJobRequest request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = HttpContext.GetIdempotencyKey();

        var inputValidation = await createRequestValidator.ValidateAsync(request, cancellationToken);
        if (!inputValidation.IsValid)
        {
            foreach (var error in inputValidation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        var command = new CreateJobCommand(
            idempotencyKey,
            ComputeRequestHash(request),
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
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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

    private static string ComputeRequestHash(CreateJobRequest request) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonSerializerOptions.Web))));
}
