using System.Text.Json;
using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Domain.Jobs;
using Microsoft.Extensions.Logging;

namespace JobOrchestrator.Infrastructure.Jobs;

/// <summary>Sample <see cref="IJobHandler"/> demonstrating end-to-end execution with cooperative cancellation. Registered for job type <c>demo-job</c>.</summary>
public sealed class DemoJobHandler(ILogger<DemoJobHandler> logger) : IJobHandler
{
    public string JobType => "demo-job";

    public async Task<string?> HandleAsync(Job job, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting demo-job {JobId} (attempt {Attempt}) with correlation {CorrelationId}",
            job.JobId, job.Attempts, job.CorrelationId);

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        logger.LogInformation("Completed demo-job {JobId}", job.JobId);

        return JsonSerializer.Serialize(new { jobId = job.JobId, status = "ok" });
    }
}
