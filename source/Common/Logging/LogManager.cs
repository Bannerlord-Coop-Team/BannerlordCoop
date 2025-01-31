using System;
using System.Collections.Generic;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Common.Logging;

public static class LogManager
{
	public static LoggerConfiguration Configuration { get; set; } = new LoggerConfiguration();
	
	// If this is called before the Configuration is setup, logging does not work
	private static Lazy<ILogger> _logger = new Lazy<ILogger>(() => Configuration.WriteTo.Sink(new OutputSinkManager()).CreateLogger());

	public static ILogger GetLogger<T>() => _logger.Value
		.ForContext<T>();
}


public class OutputSinkManager : ILogEventSink
{
    private static List<Action<string>> Callbacks { get; } = new List<Action<string>>();

    internal OutputSinkManager() { }

    public static void AddLogCallback(Action<string> callback)
    {
        Callbacks.Add(callback);
    }

    public static bool RemoveLogCallback(Action<string> callback) => Callbacks.Remove(callback);

    public void Emit(LogEvent logEvent)
    {
        TextWriter textWriter = new StringWriter();
        logEvent.RenderMessage(textWriter);

        foreach (var callback in Callbacks)
        {
            callback(textWriter.ToString());
        }
    }
}