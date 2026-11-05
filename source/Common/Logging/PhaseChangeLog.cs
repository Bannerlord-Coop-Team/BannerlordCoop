using Serilog;

namespace Common.Logging;

/// <summary>
/// Writes a lifecycle phase to the log at Information each time it changes, so the log shows
/// how far a session got and not only the crash payload. Setting the current phase again
/// writes nothing.
/// </summary>
public class PhaseChangeLog
{
    private readonly object gate = new object();
    private readonly ILogger logger;
    private string current;

    public PhaseChangeLog(ILogger logger)
    {
        this.logger = logger;
    }

    public void Report(string phase)
    {
        lock (gate)
        {
            if (phase == current) return;
            current = phase;
        }

        logger.Information("Phase changed to {Phase}", phase);
    }
}
