using Common.Logging;
using System.Collections.Concurrent;
using Xunit;

namespace Common.Tests.Logging;

public class PhaseChangeLogTests : IDisposable
{
    private readonly ConcurrentQueue<string> logs = new ConcurrentQueue<string>();
    private readonly Action<string> captureLog;
    private readonly PhaseChangeLog phaseLog = new PhaseChangeLog(LogManager.GetLogger<PhaseChangeLogTests>());

    // Other tests log through the same sink in parallel, so each test names its own phases.
    private readonly string marker = Guid.NewGuid().ToString("N");

    public PhaseChangeLogTests()
    {
        captureLog = logs.Enqueue;
        OutputSinkManager.AddLogCallback(captureLog);
    }

    public void Dispose() => OutputSinkManager.RemoveLogCallback(captureLog);

    [Fact]
    public void Report_LogsThePhase()
    {
        phaseLog.Report($"connecting-{marker}");

        Assert.Equal($"Phase changed to \"connecting-{marker}\"", Assert.Single(PhaseLines()));
    }

    [Fact]
    public void Report_SamePhaseTwice_LogsOnce()
    {
        phaseLog.Report($"applying-patches-{marker}");
        phaseLog.Report($"applying-patches-{marker}");

        Assert.Single(PhaseLines());
    }

    [Fact]
    public void Report_PhaseSetAgainAfterAnother_LogsEachChange()
    {
        phaseLog.Report($"idle-{marker}");
        phaseLog.Report($"starting-client-{marker}");
        phaseLog.Report($"idle-{marker}");

        Assert.Collection(
            PhaseLines(),
            line => Assert.Equal($"Phase changed to \"idle-{marker}\"", line),
            line => Assert.Equal($"Phase changed to \"starting-client-{marker}\"", line),
            line => Assert.Equal($"Phase changed to \"idle-{marker}\"", line));
    }

    private string[] PhaseLines() => logs.Where(line => line.Contains(marker)).ToArray();
}
