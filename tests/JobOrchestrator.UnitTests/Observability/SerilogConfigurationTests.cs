using System.Text;
using FluentAssertions;
using JobOrchestrator.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace JobOrchestrator.UnitTests.Observability;

/// <summary>
/// Proves the specific wiring in <see cref="SerilogConfiguration.Configure"/>: minimum level
/// resolution from config, <c>Enrich.FromLogContext()</c> (so ambient properties pushed via
/// <see cref="LogContext.PushProperty"/> appear on every log line, per FR-006-2/FR-006-3), and a
/// compact JSON sink (FR-006-1/AC-006-2). Not an exhaustive Serilog test.
/// </summary>
public class SerilogConfigurationTests
{
    [Fact]
    public void Configure_WithLogContextProperty_EmitsCompactJsonContainingProperty()
    {
        var appConfiguration = new ConfigurationBuilder().Build();
        var output = new StringBuilder();

        using var logger = SerilogConfiguration
            .Configure(
                new LoggerConfiguration(),
                appConfiguration,
                configureSink: sinkConfig => sinkConfig.Sink(new StringBuilderSink(output, new CompactJsonFormatter())))
            .CreateLogger();

        using (LogContext.PushProperty("CorrelationId", "abc-123"))
        {
            logger.Information("Job {JobId} accepted", Guid.Empty);
        }

        var json = output.ToString().Trim();

        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"CorrelationId\":\"abc-123\"");

        var parsed = System.Text.Json.JsonDocument.Parse(json);
        parsed.RootElement.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public void Configure_WithoutMinimumLevelConfig_DefaultsToInformation_AndSuppressesDebug()
    {
        var appConfiguration = new ConfigurationBuilder().Build();
        var output = new StringBuilder();

        using var logger = SerilogConfiguration
            .Configure(
                new LoggerConfiguration(),
                appConfiguration,
                configureSink: sinkConfig => sinkConfig.Sink(new StringBuilderSink(output, new CompactJsonFormatter())))
            .CreateLogger();

        logger.Debug("This should not appear");
        logger.Information("This should appear");

        var text = output.ToString();

        text.Should().NotContain("This should not appear");
        text.Should().Contain("This should appear");
    }

    [Fact]
    public void Configure_WithConfiguredMinimumLevel_HonorsConfigValue()
    {
        var appConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:MinimumLevel"] = "Warning",
            })
            .Build();
        var output = new StringBuilder();

        using var logger = SerilogConfiguration
            .Configure(
                new LoggerConfiguration(),
                appConfiguration,
                configureSink: sinkConfig => sinkConfig.Sink(new StringBuilderSink(output, new CompactJsonFormatter())))
            .CreateLogger();

        logger.Information("This should be suppressed at Warning level");
        logger.Warning("This should appear");

        var text = output.ToString();

        text.Should().NotContain("This should be suppressed at Warning level");
        text.Should().Contain("This should appear");
    }

    /// <summary>Minimal <see cref="ILogEventSink"/> that renders each event via the given formatter into a shared <see cref="StringBuilder"/>, avoiding a dependency on a dedicated text-writer sink package.</summary>
    private sealed class StringBuilderSink(StringBuilder output, Serilog.Formatting.ITextFormatter formatter) : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            using var writer = new StringWriter(output);
            formatter.Format(logEvent, writer);
        }
    }
}
