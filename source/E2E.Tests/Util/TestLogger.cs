using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit.Abstractions;

namespace E2E.Tests.Util;
internal class TestLogger : ILogger
{
    private readonly ITestOutputHelper output;

    public TestLogger(ITestOutputHelper output)
    {
        this.output = output;
    }

    public void Write(LogEvent logEvent)
    {
        output.WriteLine(logEvent.RenderMessage());
    }
}
