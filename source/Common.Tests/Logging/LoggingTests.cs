using Common.Logging;
using System.Collections.Concurrent;
using Xunit;

namespace Common.Tests.Logging;

public class LoggingTests
{
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

    [Fact]
    public void AddLogCallback_WithMinimumLevel_SkipsLowerLevels()
    {
        var messages = new ConcurrentQueue<string>();
        Action<string> callback = messages.Enqueue;
        OutputSinkManager.AddLogCallback(callback, Serilog.Events.LogEventLevel.Error);
        try
        {
            var logger = LogManager.GetLogger<LoggingTests>();
            logger.Warning("fixture warning below the minimum level");
            logger.Error("fixture error at the minimum level");
            Assert.DoesNotContain(messages, message => message.Contains("fixture warning below the minimum level"));
            Assert.Contains(messages, message => message.Contains("fixture error at the minimum level"));
        }
        finally
        {
            Assert.True(OutputSinkManager.RemoveLogCallback(callback));
        }
    }
}
