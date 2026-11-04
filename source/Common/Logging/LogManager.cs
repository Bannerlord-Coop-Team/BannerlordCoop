using Common.Logging.Enrichers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;

namespace Common.Logging;

public static class LogManager
{
	public static LoggerConfiguration Configuration { get; set; } = new LoggerConfiguration();
	
	private static readonly LoggingLevelSwitch RootLevel = new LoggingLevelSwitch(LogEventLevel.Verbose);

	// Built from Configuration on the first event, not the first GetLogger, because a static Logger field can be
	// initialized before the mod adds its file sink (parsing /autoconnect host:port touches CoopConnectMenuVM first).
	private static Lazy<ILogger> _logger = new Lazy<ILogger>(() =>
    {
        var configuration = Configuration
            .Enrich.With(new NetworkEnricher())
            .Enrich.With(new StackTraceEnricher())
            .WriteTo.Sink(new OutputSinkManager());

        // CI keeps its test output without buffering events for an absent Seq server.
        if (Environment.GetEnvironmentVariable("COOP_DISABLE_SEQ") != "1")
            configuration.WriteTo.Seq("http://localhost:5341");

        var logger = configuration.CreateLogger();
        // Stop building events below the configured minimum level
        RootLevel.MinimumLevel = LowestEnabledLevel(logger);
        return logger;
    });

	private static readonly ILogger Root = new LoggerConfiguration()
		.MinimumLevel.ControlledBy(RootLevel)
		.WriteTo.Sink(new ConfiguredLoggerSink())
		.CreateLogger();

	public static ILogger GetLogger<T>() => Root
		.ForContext<T>();

	// For static classes, which cannot be used as the generic type argument above.
	public static ILogger GetLogger(Type type) => Root
		.ForContext(type);

	private static LogEventLevel LowestEnabledLevel(ILogger logger)
	{
		for (var level = LogEventLevel.Verbose; level < LogEventLevel.Fatal; level++)
		{
			if (logger.IsEnabled(level)) return level;
		}

		return LogEventLevel.Fatal;
	}

	private sealed class ConfiguredLoggerSink : ILogEventSink
	{
		public void Emit(LogEvent logEvent) => _logger.Value.Write(logEvent);
	}
}