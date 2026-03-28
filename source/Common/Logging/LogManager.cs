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
		.Enrich.With(new Enricher())
		.WriteTo.Sink(new OutputSinkManager())
		.WriteTo.Seq("http://localhost:5341")
		.CreateLogger());

	public static ILogger GetLogger<T>() => _logger.Value
		.ForContext<T>();

}

class Enricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var property = ModInformation.IsServer ? "Server" : "Client";
        var logProperty = propertyFactory.CreateProperty("InstanceType", property);

		if (logEvent.Level >= LogEventLevel.Error)
		{
			var stacktraceProperty = propertyFactory.CreateProperty("StackTrace", Environment.StackTrace);
			logEvent.AddPropertyIfAbsent(stacktraceProperty);
		}

        logEvent.AddPropertyIfAbsent(logProperty);
    }
}