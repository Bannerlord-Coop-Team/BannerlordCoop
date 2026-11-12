using Common.Logging.Enrichers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Concurrent;

namespace Common.Logging;

public static class LogManager
{
	public static LoggerConfiguration Configuration { get; set; } = new LoggerConfiguration();
	
	// Built from Configuration on the first event, not the first GetLogger, because a static Logger field can be
	// initialized before the mod adds its file sink (parsing /autoconnect host:port touches CoopConnectMenuVM first).
	// Configuration is still frozen at that first event, so finish configuring before anything logs.
	private static Lazy<ILogger> _logger = new Lazy<ILogger>(() =>
    {
        var configuration = Configuration
            .Enrich.With(new NetworkEnricher())
            .Enrich.With(new StackTraceEnricher())
            .WriteTo.Sink(new OutputSinkManager());

        // CI keeps its test output without buffering events for an absent Seq server.
        if (Environment.GetEnvironmentVariable("COOP_DISABLE_SEQ") != "1")
            configuration.WriteTo.Seq("http://localhost:5341");

        return configuration.CreateLogger();
    });

	// One stand in logger per source, so the configured logger's MinimumLevel.Override for that source still applies
	private static readonly ConcurrentDictionary<string, ILogger> SourceLoggers = new ConcurrentDictionary<string, ILogger>();

	public static ILogger GetLogger<T>() => GetLogger(typeof(T));

	// For static classes, which cannot be used as the generic type argument above.
	public static ILogger GetLogger(Type type) => SourceLoggers.GetOrAdd(type.FullName, CreateSourceLogger);

	private static ILogger CreateSourceLogger(string source)
	{
		var level = new LoggingLevelSwitch(LogEventLevel.Verbose);
		return new LoggerConfiguration()
			.MinimumLevel.ControlledBy(level)
			.WriteTo.Sink(new ConfiguredLoggerSink(source, level))
			.CreateLogger()
			.ForContext(Constants.SourceContextPropertyName, source);
	}

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
		private readonly Lazy<ILogger> _sourceLogger;

		public ConfiguredLoggerSink(string source, LoggingLevelSwitch level)
		{
			_sourceLogger = new Lazy<ILogger>(() =>
			{
				// ForContext on the configured logger picks up its level override for this source
				var logger = _logger.Value.ForContext(Constants.SourceContextPropertyName, source);
				// Stop building events below the level this source is configured for
				level.MinimumLevel = LowestEnabledLevel(logger);
				return logger;
			});
		}

		public void Emit(LogEvent logEvent) => _sourceLogger.Value.Write(logEvent);
	}
}