using Serilog;
using Serilog.Core;
using System;
using System.IO;

namespace Common.Logging;

/// <summary>
/// Opens this session's log file, keeping the last session's log as <c>.prev.log</c> so a crashed session's log
/// survives one relaunch, and falling back to the process-id log when the canonical one won't open.
/// </summary>
public interface ILogFileOpener
{
    /// <summary>The path the log opened at, or null when no log file opened.</summary>
    string OpenedPath { get; }

    /// <summary>Opens the log with <paramref name="createSink"/>, or returns null when no path opens.</summary>
    ILogEventSink Open(Func<string, ILogEventSink> createSink);

    /// <summary>Logs what <see cref="Open"/> could not do, once logging is configured.</summary>
    void LogFailures(ILogger logger);
}

/// <inheritdoc />
public class LogFileOpener : ILogFileOpener
{
    private readonly string logPath;
    private readonly string fallbackPath;
    private readonly string previousLogPath;
    private Exception keepFailure;
    private Exception openFailure;

    /// <summary>Picks the canonical log, or the process-id log with no fallback when another live process claimed the canonical one.</summary>
    public LogFileOpener(string canonicalLogPath, string processLogPath, bool canonicalLogClaimed)
    {
        logPath = canonicalLogClaimed ? canonicalLogPath : processLogPath;
        fallbackPath = canonicalLogClaimed ? processLogPath : null;
        previousLogPath = Path.ChangeExtension(logPath, ".prev.log");
    }

    /// <inheritdoc />
    public string OpenedPath { get; private set; }

    /// <inheritdoc />
    public ILogEventSink Open(Func<string, ILogEventSink> createSink)
    {
        keepFailure = KeepPreviousLog();

        var sink = TryOpen(logPath, createSink);
        if (sink != null || fallbackPath == null) return sink;

        // A program holding the log open (an editor, a backup tool) makes the sink throw, which stopped start-up.
        return TryOpen(fallbackPath, createSink);
    }

    /// <inheritdoc />
    public void LogFailures(ILogger logger)
    {
        if (keepFailure != null)
            logger.Warning(keepFailure, "Previous log {Path} could not be kept", previousLogPath);

        if (openFailure == null) return;

        if (OpenedPath == null)
            logger.Warning(openFailure, "No log file could be opened; this session writes no log file");
        else
            logger.Warning(openFailure, "Log {Path} could not be opened; this session logs to {OpenedPath}", logPath, OpenedPath);
    }

    // Moves the last session's log to its .prev.log path, replacing an older one, and returns the failure or null.
    private Exception KeepPreviousLog()
    {
        if (!File.Exists(logPath)) return null;

        try
        {
            // Delete then move rather than File.Replace, which fails while a reader such as tail -f holds the log.
            File.Delete(previousLogPath);
            File.Move(logPath, previousLogPath);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception;
        }
    }

    // Clears the path and opens a sink on it, or keeps the failure and returns null when the file won't open.
    private ILogEventSink TryOpen(string path, Func<string, ILogEventSink> createSink)
    {
        try
        {
            // A file left at the path, such as an earlier same-id process's log, would otherwise be appended to.
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort, the open below reports a file that can't be used.
        }

        try
        {
            var sink = createSink(path);
            OpenedPath = path;
            return sink;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            openFailure = exception;
            return null;
        }
    }
}
