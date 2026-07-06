using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JobOrchestrator.Application.Abstractions;
using JobOrchestrator.Domain.Jobs;
using JobOrchestrator.Infrastructure.Persistence;
using JobOrchestrator.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace JobOrchestrator.IntegrationTests.IngestionApi;

/// <summary>
/// End-to-end coverage of specs/002-ingestion-api's acceptance criteria against the real Api
/// host + a Testcontainers Mongo replica set (AC-002-1, -2, -3, -4, -5, -6, -7).
/// </summary>
public sealed class JobEndpointsTests : IClassFixture<IngestionApiFactory>
{
    private readonly IngestionApiFactory _factory;

    public JobEndpointsTests(IngestionApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", IngestionApiFactory.ApiKey);
        return client;
    }

    private static StringContent JsonBody(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static object ValidRequestBody(JobTypes type = JobTypes.Demo) => new
    {
        type,
        priority = "Low",
        maxAttempts = 5,
        payload = new { to = "a@b.com" },
    };

    // AC-002-1
    [Fact]
    public async Task PostJobs_WithoutCredentials_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/jobs", JsonBody(ValidRequestBody()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetJob_WithoutCredentials_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-002-2
    [Fact]
    public async Task PostJobs_DuplicateIdempotencyKey_SameBody_CreatesOneJobReturnsSameId()
    {
        var client = CreateAuthenticatedClient();
        var idempotencyKey = Guid.NewGuid().ToString();
        var body = ValidRequestBody(JobTypes.Demo);

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/jobs") { Content = JsonBody(body) };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/jobs") { Content = JsonBody(body) };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await client.SendAsync(request2);

        response1.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response2.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var json1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        var json2 = await response2.Content.ReadFromJsonAsync<JsonElement>();
        json1.GetProperty("jobId").GetGuid().Should().Be(json2.GetProperty("jobId").GetGuid());

        var jobId = json1.GetProperty("jobId").GetGuid();
        var database = _factory.GetDatabase();
        var count = await database.GetCollection<JobDocument>(MongoCollectionNames.Jobs)
            .CountDocumentsAsync(d => d.JobId == jobId);
        count.Should().Be(1);
    }

    // AC-002-3
    [Fact]
    public async Task PostJobs_DuplicateIdempotencyKey_DifferentBody_Returns409()
    {
        var client = CreateAuthenticatedClient();
        var idempotencyKey = Guid.NewGuid().ToString();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/jobs") { Content = JsonBody(ValidRequestBody(JobTypes.Demo)) };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/jobs") { Content = JsonBody(ValidRequestBody(JobTypes.DemoDelayed)) };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await client.SendAsync(request2);

        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // AC-002-4
    [Fact]
    public async Task PostJobs_MissingType_Returns400()
    {
        var client = CreateAuthenticatedClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/jobs")
        {
            Content = JsonBody(new { priority = "Low", payload = new { } }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostJobs_MissingIdempotencyKeyHeader_Returns400()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsync("/jobs", JsonBody(ValidRequestBody()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-002-5
    [Fact]
    public async Task PostJobs_ValidRequest_Returns202AndPersistsJobAndOutboxAtomically()
    {
        var client = CreateAuthenticatedClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/jobs") { Content = JsonBody(ValidRequestBody(JobTypes.Demo)) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Location.Should().NotBeNull();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = json.GetProperty("jobId").GetGuid();

        var database = _factory.GetDatabase();
        var jobCount = await database.GetCollection<JobDocument>(MongoCollectionNames.Jobs)
            .CountDocumentsAsync(d => d.JobId == jobId);
        jobCount.Should().Be(1);

        var outboxCount = await database.GetCollection<OutboxMessageDocument>(MongoCollectionNames.Outbox)
            .CountDocumentsAsync(d => d.JobId == jobId);
        outboxCount.Should().Be(1);
    }

    // AC-002-8: correlation id flows onto the persisted job
    [Fact]
    public async Task PostJobs_WithCorrelationIdHeader_PersistsItOnTheJob()
    {
        var client = CreateAuthenticatedClient();
        var correlationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/jobs") { Content = JsonBody(ValidRequestBody(JobTypes.Demo)) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.GetValues("X-Correlation-Id").Should().Contain(correlationId);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = json.GetProperty("jobId").GetGuid();

        var database = _factory.GetDatabase();
        var document = await database.GetCollection<JobDocument>(MongoCollectionNames.Jobs)
            .Find(d => d.JobId == jobId).FirstOrDefaultAsync();
        document.CorrelationId.Should().Be(correlationId);
    }

    // AC-002-6
    [Fact]
    public async Task GetJob_UnknownId_Returns404()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync($"/jobs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetJob_ExistingJob_Returns200WithStatus()
    {
        var client = CreateAuthenticatedClient();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/jobs") { Content = JsonBody(ValidRequestBody(JobTypes.Demo)) };
        createRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var createResponse = await client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("jobId").GetGuid();

        var response = await client.GetAsync($"/jobs/{jobId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("jobId").GetGuid().Should().Be(jobId);
        json.GetProperty("status").GetString().Should().Be(nameof(JobStatus.Queued));
    }

    // AC-002-7
    [Fact]
    public async Task CancelJob_FreshlyCreatedQueuedJob_Returns202()
    {
        var client = CreateAuthenticatedClient();
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/jobs") { Content = JsonBody(ValidRequestBody(JobTypes.Demo)) };
        createRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var createResponse = await client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = created.GetProperty("jobId").GetGuid();

        var response = await client.PostAsync($"/jobs/{jobId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task CancelJob_AlreadyCompletedJob_Returns409()
    {
        // No worker runs in these tests, so drive a job straight to Completed via the repository.
        var database = _factory.GetDatabase();
        var sessionAccessor = new MongoSessionAccessor();
        var jobRepository = new MongoJobRepository(database, sessionAccessor);

        var now = DateTimeOffset.UtcNow;
        var job = Job.Create(Guid.NewGuid(), Guid.NewGuid().ToString(), JobTypes.Demo, "{}",
            Priority.Low, null, 3, Guid.NewGuid().ToString(), now);
        job.MarkQueued(now);
        job.StartProcessing(now);
        job.Complete(null, now);
        await jobRepository.AddAsync(job, CancellationToken.None);

        var client = CreateAuthenticatedClient();

        var response = await client.PostAsync($"/jobs/{job.JobId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelJob_UnknownId_Returns404()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsync($"/jobs/{Guid.NewGuid()}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
