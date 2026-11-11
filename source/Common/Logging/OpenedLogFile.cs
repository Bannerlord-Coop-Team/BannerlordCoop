using Serilog;
using System;

namespace Common.Logging;

/// <summary>
/// The log file <see cref="ILogFileOpener.Open"/> opened and what it could not do, logged once logging is configured.
/// </summary>
public sealed class OpenedLogFile
{
    private readonly string logPath;
    private readonly Exception keepFailure;
    private readonly Exception openFailure;

    /// <summary>Holds the opened path (null when none opened) and the failures to keep and open <paramref name="logPath"/>.</summary>
    public OpenedLogFile(string path, string logPath, Exception keepFailure, Exception openFailure)
    {
        Path = path;
        this.logPath = logPath;
        this.keepFailure = keepFailure;
        this.openFailure = openFailure;
    }

    /// <summary>The path the log opened at, or null when no log file opened.</summary>
    public string Path { get; }

    /// <summary>Logs each failure as a warning.</summary>
    public void LogFailures(ILogger logger)
    {
        if (keepFailure != null)
            logger.Warning(keepFailure, "Log {Path} could not be kept as the previous log", logPath);

        if (openFailure == null) return;

        if (Path == null)
            logger.Warning(openFailure, "No log file could be opened; this session writes no log file");
        else
            logger.Warning(openFailure, "Log {Path} could not be opened; this session logs to {OpenedPath}", logPath, Path);
    }
}
