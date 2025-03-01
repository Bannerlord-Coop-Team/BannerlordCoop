using Serilog;
using System;

namespace Common.Logging;

public static class LogManager
{
	public static LoggerConfiguration Configuration { get; set; } = new LoggerConfiguration();
	
	// If this is called before the Configuration is setup, logging does not work
	private static Lazy<ILogger> _logger = new Lazy<ILogger>(() => Configuration.WriteTo.Sink(new OutputSinkManager()).CreateLogger());

	public static ILogger GetLogger<T>() => _logger.Value
		.ForContext<T>();
}