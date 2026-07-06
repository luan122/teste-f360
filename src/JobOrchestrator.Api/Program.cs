using JobOrchestrator.Api.DependencyInjection;
using JobOrchestrator.Api.Middleware;
using JobOrchestrator.Application.DependencyInjection;
using JobOrchestrator.Infrastructure.DependencyInjection;
using JobOrchestrator.Infrastructure.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    SerilogConfiguration.Configure(configuration, context.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIngestionApi(builder.Configuration);
builder.Services.AddObservability(
    builder.Configuration,
    serviceName: "JobOrchestrator.Api",
    configureTracing: b => b.AddAspNetCoreInstrumentation());

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ValidationExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapControllers();

app.Run();

public partial class Program;
