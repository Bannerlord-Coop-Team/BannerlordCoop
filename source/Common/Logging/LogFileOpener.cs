using Serilog;
using System;
using System.IO;
using System.Linq;

namespace Common.Logging;

/// <summary>
/// Picks and opens this process's log file, <c>Coop_&lt;role&gt;.log</c> or a process-id log when that is taken,
/// keeping the last session's log as <c>.prev.log</c>; disposing releases the claim on the canonical log.
/// </summary>
public interface ILogFileOpener : IDisposable
{
    /// <summary>Runs <paramref name="openSink"/> on the log path and returns the path it opened, or null when none opened.</summary>
    string Open(Action<string> openSink);

    /// <summary>Logs what <see cref="Open"/> could not do, once logging is configured.</summary>
    void LogFailures(ILogger logger);
}

/// <inheritdoc />
public class LogFileOpener : ILogFileOpener
{
    private const int KeptProcessLogs = 5;

    private readonly string directory;
    private readonly string processLogPattern;
    private readonly string canonicalLogPath;
    private readonly string processLogPath;
    private FileStream canonicalLogClaim;
    private string logPath;
    private string openedPath;
    private Exception keepFailure;
    private Exception openFailure;

    /// <summary>Names the role's canonical and process-id logs in <paramref name="directory"/>.</summary>
    public LogFileOpener(string directory, string role, int processId)
    {
        this.directory = directory;
        processLogPattern = $"Coop_{role}_*.log";
        canonicalLogPath = Path.Combine(directory, $"Coop_{role}.log");
        processLogPath = Path.Combine(directory, $"Coop_{role}_{processId}.log");
    }

    private string PreviousLogPath => Path.ChangeExtension(logPath, ".prev.log");

    /// <inheritdoc />
    public string Open(Action<string> openSink)
    {
        var canonicalLogClaimed = TryClaimCanonicalLog();
        PruneProcessLogs();

        // Another live process owns the canonical log, so this one logs under its process id.
        logPath = canonicalLogClaimed ? canonicalLogPath : processLogPath;
        keepFailure = KeepPreviousLog();

        openedPath = TryOpen(logPath, openSink);
        if (openedPath != null || !canonicalLogClaimed) return openedPath;

        // A program holding the log open (an editor, a backup tool) makes the sink throw, so the process-id log takes over.
        openedPath = TryOpen(processLogPath, openSink);
        return openedPath;
    }

    /// <inheritdoc />
    public void LogFailures(ILogger logger)
    {
        if (keepFailure != null)
            logger.Warning(keepFailure, "Previous log {Path} could not be kept", PreviousLogPath);

        if (openFailure == null) return;

        if (openedPath == null)
            logger.Warning(openFailure, "No log file could be opened; this session writes no log file");
        else
            logger.Warning(openFailure, "Log {Path} could not be opened; this session logs to {OpenedPath}", logPath, openedPath);
    }

    /// <summary>Releases the claim on the canonical log.</summary>
    public void Dispose() => canonicalLogClaim?.Dispose();

    // Holds an exclusive sidecar lock until disposed, because File.Delete succeeds on a log another process has open
    // and a check-then-release probe leaves a window where a second instance's probe also succeeds.
    private bool TryClaimCanonicalLog()
    {
        try
        {
            canonicalLogClaim = new FileStream(canonicalLogPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // Keeps only the newest process-id logs, which pile up because each dual-client run adds one under a fresh pid.
    private void PruneProcessLogs()
    {
        try
        {
            // The extension filter skips .log.lock sidecars, which .NET also matches for a 3-character pattern extension.
            var staleLogs = Directory.GetFiles(directory, processLogPattern)
                .Where(file => Path.GetExtension(file).Equals(".log", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Skip(KeptProcessLogs);
            foreach (var file in staleLogs)
            {
                try { File.Delete(file); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { /* still open by a live instance */ }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort, housekeeping never stops start-up.
        }
    }

    // Moves the last session's log to its .prev.log path, replacing an older one, and returns the failure or null.
    private Exception KeepPreviousLog()
    {
        if (!File.Exists(logPath)) return null;

        try
        {
            // Delete then move rather than File.Replace, which fails while a reader such as tail -f holds the log.
            File.Delete(PreviousLogPath);
            File.Move(logPath, PreviousLogPath);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception;
        }
    }

    // Clears the path and opens a sink on it, returning the path, or keeps the failure and returns null.
    private string TryOpen(string path, Action<string> openSink)
    {
        try
        {
            // A file left at the path, such as an earlier same-id process's log, would otherwise be appended to.
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort, a file that can't be deleted is appended to, or fails the open below.
        }

        try
        {
            openSink(path);
            return path;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            openFailure = exception;
            return null;
        }
    }
}
