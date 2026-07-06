using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace JobOrchestrator.Infrastructure.Observability;

/// <summary>Shared Serilog configuration for both hosts, producing structured compact-JSON console output enriched from <see cref="Serilog.Context.LogContext"/>.</summary>
public static class SerilogConfiguration
{
    private const string MinimumLevelConfigKey = "Serilog:MinimumLevel";

    /// <summary>
    /// Applies the shared logging configuration (minimum level, LogContext enrichment, compact
    /// JSON console sink) to <paramref name="loggerConfiguration"/>.
    /// </summary>
    /// <param name="loggerConfiguration">The <see cref="LoggerConfiguration"/> to configure.</param>
    /// <param name="appConfiguration">
    /// Application configuration used to resolve <c>Serilog:MinimumLevel</c> (defaults to
    /// <see cref="LogEventLevel.Information"/> when absent or unparsable).
    /// </param>
    /// <param name="configureSink">
    /// Optional seam to override where the compact JSON output is written. Defaults to the
    /// console. Tests can supply a sink (e.g. a <c>TextWriter</c>-backed sink) to capture output
    /// without touching <see cref="Console"/>.
    /// </param>
    public static LoggerConfiguration Configure(
        LoggerConfiguration loggerConfiguration,
        IConfiguration appConfiguration,
        Action<LoggerSinkConfiguration>? configureSink = null)
    {
        ArgumentNullException.ThrowIfNull(loggerConfiguration);
        ArgumentNullException.ThrowIfNull(appConfiguration);

        var minimumLevel = ResolveMinimumLevel(appConfiguration);

        loggerConfiguration
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext();

        if (configureSink is not null)
        {
            configureSink(loggerConfiguration.WriteTo);
        }
        else
        {
            loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
        }

        return loggerConfiguration;
    }

    private static LogEventLevel ResolveMinimumLevel(IConfiguration appConfiguration)
    {
        var configuredValue = appConfiguration[MinimumLevelConfigKey];

        return Enum.TryParse<LogEventLevel>(configuredValue, ignoreCase: true, out var level)
            ? level
            : LogEventLevel.Information;
    }
}
