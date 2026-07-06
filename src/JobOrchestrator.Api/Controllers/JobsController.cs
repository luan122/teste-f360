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
    IValidator<CreateJobRequest> validator) : ControllerBase
{
    /// <summary>Submits a new job for durable processing.</summary>
    [HttpPost]
    [RequiresIdempotencyKey]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(JobAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateJob(
        [FromBody] CreateJobRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            return ValidationProblem();
        }

        var idempotencyKey = HttpContext.GetIdempotencyKey();
        var result = await sender.Send(new CreateJobCommand(
            IdempotencyKey: idempotencyKey,
            RequestHash: ComputeHash(request),
            Type: request.Type,
            Priority: request.ParsePriority(),
            Payload: request.SerializePayload(),
            ScheduledAt: request.ScheduledAt,
            MaxAttempts: request.ResolveMaxAttempts(),
            CorrelationId: HttpContext.GetCorrelationId()),
            cancellationToken);

        if (result.IsIdempotencyConflict)
            return Problem(
                detail: $"Idempotency-Key '{idempotencyKey}' was already used with a different payload.",
                statusCode: StatusCodes.Status409Conflict);

        return AcceptedAtAction(
            actionName: nameof(GetJob),
            controllerName: "Jobs",
            routeValues: new { id = result.Job.JobId },
            value: new JobAcceptedResponse(result.Job.JobId, result.Job.Status.ToString(), result.Job.CorrelationId));
    }

    /// <summary>Returns the current status of a job.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(Guid id, CancellationToken cancellationToken)
    {
        var dto = await sender.Send(new GetJobStatusQuery(id), cancellationToken);

        return dto is null
            ? Problem(detail: $"Job '{id}' was not found.", statusCode: StatusCodes.Status404NotFound)
            : Ok(JobStatusResponse.FromDto(dto));
    }

    /// <summary>Requests cooperative cancellation of a job.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelJob(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(new CancelJobCommand(id), cancellationToken);
            return result.Status == CancelJobStatus.NotFound
                ? Problem(detail: $"Job '{id}' was not found.", statusCode: StatusCodes.Status404NotFound)
                : Accepted();
        }
        catch (InvalidJobStateTransitionException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static string ComputeHash(CreateJobRequest request) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonSerializerOptions.Web))));
}
