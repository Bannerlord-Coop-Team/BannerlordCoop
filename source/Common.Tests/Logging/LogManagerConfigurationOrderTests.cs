using Common.Logging;
using Serilog;
using Serilog.Core;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace Common.Tests.Logging;

public class LogManagerConfigurationOrderTests
{
    [Fact]
    public void FileSinkAddedAfterGetLogger_ReceivesLogLines()
    {
        RunWithFreshLogManager((logManager, addFileSink) =>
        {
            // What FpsLogger's static field does when its initializer runs before CoopMod.SetupLogging
            var logger = GetLogger(logManager);
            var filePath = addFileSink();

            logger.Information("logged after setup marker");

            Assert.Contains("logged after setup marker", ReadShared(filePath));
        });
    }

    [Fact]
    public void FileSinkAddedBeforeGetLogger_ReceivesLogLines()
    {
        RunWithFreshLogManager((logManager, addFileSink) =>
        {
            var filePath = addFileSink();
            var logger = GetLogger(logManager);

            logger.Information("configured first marker");

            Assert.Contains("configured first marker", ReadShared(filePath));
        });
    }

    // LogManager is process-wide static state, so each test loads its own copy of Common.dll
    private static void RunWithFreshLogManager(Action<Type, Func<string>> test)
    {
        var context = new AssemblyLoadContext(nameof(LogManagerConfigurationOrderTests), isCollectible: true);
        var directory = Path.Combine(Path.GetTempPath(), "coop-log-order-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        IDisposable sink = null;
        try
        {
            var common = context.LoadFromAssemblyPath(typeof(LogManager).Assembly.Location);
            var logManager = common.GetType(typeof(LogManager).FullName, throwOnError: true);
            var sinkType = common.GetType(typeof(SizeLimitedFileSink).FullName, throwOnError: true);

            test(logManager, () =>
            {
                var filePath = Path.Combine(directory, "Coop_client.log");
                sink = (IDisposable)Activator.CreateInstance(
                    sinkType, filePath, "{Message:lj}{NewLine}", 30L * 1024 * 1024, 1L * 1024 * 1024, 28L * 1024 * 1024);
                var configuration = (LoggerConfiguration)logManager.GetProperty(nameof(LogManager.Configuration)).GetValue(null);
                configuration.WriteTo.Sink((ILogEventSink)sink).MinimumLevel.Information();
                return filePath;
            });
        }
        finally
        {
            sink?.Dispose();
            context.Unload();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ILogger GetLogger(Type logManager) =>
        (ILogger)logManager.GetMethod(nameof(LogManager.GetLogger), new[] { typeof(Type) })
            .Invoke(null, new object[] { typeof(LogManagerConfigurationOrderTests) });

    private static string ReadShared(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
