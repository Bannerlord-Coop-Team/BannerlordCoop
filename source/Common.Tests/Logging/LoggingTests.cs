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
}
