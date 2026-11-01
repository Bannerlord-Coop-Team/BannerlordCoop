using Common.Logging.Enrichers;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Common.Logging;

public static class LogManager
{
	public static LoggerConfiguration Configuration { get; set; } = new LoggerConfiguration();

	public static SessionSummarySink SessionSummary { get; } = new SessionSummarySink();
	
	// If this is called before the Configuration is setup, logging does not work
	private static Lazy<ILogger> _logger = new Lazy<ILogger>(() =>
    {
        var configuration = Configuration
            .Enrich.With(new NetworkEnricher())
            .Enrich.With(new StackTraceEnricher())
            .WriteTo.Sink(new OutputSinkManager())
            .WriteTo.Sink(SessionSummary);

        // CI keeps its test output without buffering events for an absent Seq server.
        if (Environment.GetEnvironmentVariable("COOP_DISABLE_SEQ") != "1")
            configuration.WriteTo.Seq("http://localhost:5341");

        return configuration.CreateLogger();
    });

	public static ILogger GetLogger<T>() => _logger.Value
		.ForContext<T>();

	// For static classes, which cannot be used as the generic type argument above.
	public static ILogger GetLogger(Type type) => _logger.Value
		.ForContext(type);

	/// <summary>
	/// Disposes the logger that GetLogger hands out, which flushes the sinks that buffer (Seq), and waits
	/// at most <paramref name="limit"/>. Serilog.Log.CloseAndFlush does not reach this logger, because it is
	/// never Serilog.Log.Logger. Call it only when the process ends: later events are dropped.
	/// </summary>
	/// <returns>false when the sinks did not close within the limit, or one threw while it closed.</returns>
	public static bool CloseAndFlush(TimeSpan limit) => CloseAndFlush(_logger, limit);

	internal static bool CloseAndFlush(Lazy<ILogger> logger, TimeSpan limit)
	{
		// Never build the logger only to dispose it.
		if (!logger.IsValueCreated || logger.Value is not IDisposable disposable) return true;

		// With no Seq server listening, the Seq sink takes about 8 s to give up its last send, so the wait has a limit.
		try
		{
			return Task.Run(disposable.Dispose).Wait(limit);
		}
		catch (AggregateException)
		{
			return false;
		}
	}

}