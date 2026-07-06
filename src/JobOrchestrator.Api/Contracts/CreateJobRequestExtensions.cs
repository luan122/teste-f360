using JobOrchestrator.Domain.Jobs;

namespace JobOrchestrator.Api.Contracts;

/// <summary>Extension methods for mapping <see cref="CreateJobRequest"/> fields to command inputs.</summary>
public static class CreateJobRequestExtensions
{
    private const int DefaultMaxAttempts = 5;
    private const Priority DefaultPriority = Priority.Low;

    /// <summary>Returns the parsed <see cref="Priority"/> value. Assumes the value has already been validated.</summary>
    public static Priority ParsePriority(this CreateJobRequest request) =>
        string.IsNullOrWhiteSpace(request.Priority)
            ? DefaultPriority
            : Enum.Parse<Priority>(request.Priority, ignoreCase: false);

    /// <summary>Returns <see cref="CreateJobRequest.MaxAttempts"/> or the default when absent.</summary>
    public static int ResolveMaxAttempts(this CreateJobRequest request) =>
        request.MaxAttempts ?? DefaultMaxAttempts;

    /// <summary>Returns the raw JSON text of the payload, or an empty string when absent.</summary>
    public static string SerializePayload(this CreateJobRequest request) =>
        request.Payload.HasValue ? request.Payload.Value.GetRawText() : string.Empty;
}
