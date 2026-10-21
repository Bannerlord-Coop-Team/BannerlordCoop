using Common.Logging;
using System.Collections.Concurrent;
using Xunit;
using Serilog;

namespace Common.Tests.Logging;

public class LoggingTests
{
#if DEBUG
    [Fact]
    public void DedicatedLiveTest_WritesDiagnosticsWithoutConsoleOrCoopMod()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".log");
        var modPath = Path.ChangeExtension(path, ".coop.log");
        try
        {
            File.WriteAllText(path, "launcher banner");
            var configuration = new LoggerConfiguration();
            LogManager.ConfigureDedicatedLiveTestLog(configuration, new[] {
                "dotnet", "/dedicatedcustomserver", "7210", "/cooptestrun", "logging-proof", "/cooptestlog", path });
            using (var logger = configuration.CreateLogger())
                logger.Debug(new InvalidOperationException("fixture exception"), "server marker");

            var text = File.ReadAllText(modPath);
            Assert.Contains("server marker", text);
            Assert.Contains("fixture exception", text);
            Assert.Equal("launcher banner", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
            File.Delete(modPath);
        }
    }

    [Theory]
    [InlineData("/client", "/cooptestrun", "logging-proof")]
    [InlineData("/dedicatedcustomserver", "7210", "EU")]
    public void OrdinaryProcesses_DoNotCreateDedicatedLiveTestLog(string first, string second, string third)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".log");
        var configuration = new LoggerConfiguration();
        LogManager.ConfigureDedicatedLiveTestLog(configuration, new[] { first, second, third, "/cooptestlog", path });
        using (var logger = configuration.CreateLogger())
            logger.Information("ignored marker");
        Assert.False(File.Exists(Path.ChangeExtension(path, ".coop.log")));
    }
#endif

    [Fact]
    public void Error_PreservesOutputAndException()
    {
        var messages = new ConcurrentQueue<string>();
        Action<string> callback = messages.Enqueue;
        OutputSinkManager.AddLogCallback(callback);
        try
        {
            LogManager.GetLogger<LoggingTests>().Error(
                new InvalidOperationException("fixture failure detail"), "fixture diagnostic marker");
            Assert.Contains(messages, message =>
                message.Contains("fixture diagnostic marker") && message.Contains("fixture failure detail"));
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(callback);
        }
    }
}
