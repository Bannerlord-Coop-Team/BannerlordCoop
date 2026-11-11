using Serilog;
using System;
using System.Collections.Generic;

namespace Common.Logging;

/// <summary>
/// The outcome of <see cref="ILogFileOpener.Open"/>: the opened log path and the warnings to log once logging is configured.
/// </summary>
public sealed class LogFileOpenResult
{
    private readonly IReadOnlyList<Action<ILogger>> warnings;

    /// <summary>Holds the opened path, null when no log file opened, and the warnings recorded while opening.</summary>
    public LogFileOpenResult(string openedPath, IReadOnlyList<Action<ILogger>> warnings)
    {
        OpenedPath = openedPath;
        this.warnings = warnings;
    }

    /// <summary>The path the log opened at, or null when no log file opened.</summary>
    public string OpenedPath { get; }

    /// <summary>Writes the recorded warnings to <paramref name="logger"/>.</summary>
    public void LogWarnings(ILogger logger)
    {
        foreach (var warning in warnings)
            warning(logger);
    }
}
