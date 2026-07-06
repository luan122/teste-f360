using JobOrchestrator.Application.DependencyInjection;
using JobOrchestrator.Infrastructure.DependencyInjection;
using JobOrchestrator.Infrastructure.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    SerilogConfiguration.Configure(configuration, context.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWorkerProcessing(builder.Configuration);
builder.Services.AddTaskManagement(builder.Configuration);
builder.Services.AddResilience(builder.Configuration);
builder.Services.AddObservability(builder.Configuration, serviceName: "JobOrchestrator.Worker");

var app = builder.Build();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();
