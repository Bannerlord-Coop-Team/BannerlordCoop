using Common.Logging.Enrichers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;

namespace Common.Logging;

public static class LogManager
{
	public static LoggerConfiguration Configuration { get; set; } = new LoggerConfiguration();
	
	// If this is called before the Configuration is setup, logging does not work
	private static Lazy<ILogger> _logger = new Lazy<ILogger>(() => Configuration
		.Enrich.With(new NetworkEnricher())
		.Enrich.With(new StackTraceEnricher())
        .WriteTo.Sink(new OutputSinkManager())
		.WriteTo.Seq("http://localhost:5341")
		.CreateLogger());

	public static ILogger GetLogger<T>() => _logger.Value
		.ForContext<T>();

	// Type overload for callers that cannot use the generic form, such as static classes
	// (a static type cannot be a generic type argument).
	public static ILogger GetLogger(Type type) => _logger.Value
		.ForContext(type);

}