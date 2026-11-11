using Serilog;

namespace Common.Logging;

/// <summary>
/// Writes a lifecycle phase to the log at Information each time it changes, so the log shows how far a session got.
/// </summary>
public interface IPhaseChangeLog
{
    /// <summary>Logs the phase unless it is already the current one.</summary>
    void Report(string phase);
}

/// <inheritdoc />
public class PhaseChangeLog : IPhaseChangeLog
{
    private readonly object gate = new object();
    private readonly ILogger logger;
    private string current;

    public PhaseChangeLog(ILogger logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
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
