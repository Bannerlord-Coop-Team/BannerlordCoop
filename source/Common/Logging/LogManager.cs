using Common.Logging.Enrichers;
using Serilog;
using System;
using System.IO;

namespace Common.Logging;

public static class LogManager
{
	public static LoggerConfiguration Configuration { get; set; } = new LoggerConfiguration();
	
	// If this is called before the Configuration is setup, logging does not work
	private static Lazy<ILogger> _logger = new Lazy<ILogger>(() =>
    {
        var configuration = Configuration
            .Enrich.With(new NetworkEnricher())
            .Enrich.With(new StackTraceEnricher())
            .WriteTo.Sink(new OutputSinkManager());

#if DEBUG
        ConfigureDedicatedLiveTestLog(configuration, Environment.GetCommandLineArgs());
#endif

        // CI keeps its test output without buffering events for an absent Seq server.
        if (Environment.GetEnvironmentVariable("COOP_DISABLE_SEQ") != "1")
            configuration.WriteTo.Seq("http://localhost:5341");

        return configuration.CreateLogger();
    });

#if DEBUG
    internal static void ConfigureDedicatedLiveTestLog(LoggerConfiguration configuration, string[] arguments)
    {
        if (Array.IndexOf(arguments, "/dedicatedcustomserver") < 0 ||
            Array.IndexOf(arguments, "/cooptestrun") < 0)
            return;

        var logArgument = Array.IndexOf(arguments, "/cooptestlog");
        if (logArgument < 0 || logArgument + 1 >= arguments.Length ||
            !Path.IsPathRooted(arguments[logArgument + 1]))
            return;

        // The dedicated engine never loads CoopMod.SetupLogging; stdout may also be detached.
        var path = Path.ChangeExtension(arguments[logArgument + 1], ".coop.log");
        configuration.MinimumLevel.Debug().WriteTo.Sink(new SizeLimitedFileSink(
            path,
            "[{Timestamp:O} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}",
            30L * 1024 * 1024,
            1L * 1024 * 1024,
            28L * 1024 * 1024));
    }
#endif

	public static ILogger GetLogger<T>() => _logger.Value
		.ForContext<T>();

	// For static classes, which cannot be used as the generic type argument above.
	public static ILogger GetLogger(Type type) => _logger.Value
		.ForContext(type);

}
