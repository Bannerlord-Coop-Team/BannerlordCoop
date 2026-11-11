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
    /// <summary>Runs <paramref name="openSink"/> on the log path and returns the path it opened and what failed.</summary>
    OpenedLogFile Open(Action<string> openSink);
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

    /// <summary>Names the role's canonical and process-id logs in <paramref name="directory"/>.</summary>
    public LogFileOpener(string directory, string role, int processId)
    {
        this.directory = directory;
        processLogPattern = $"Coop_{role}_*.log";
        canonicalLogPath = Path.Combine(directory, $"Coop_{role}.log");
        processLogPath = Path.Combine(directory, $"Coop_{role}_{processId}.log");
    }

    /// <inheritdoc />
    public OpenedLogFile Open(Action<string> openSink)
    {
        var canonicalLogClaimed = TryClaimCanonicalLog();
        PruneProcessLogs();

        // Another live process owns the canonical log, so this one logs under its process id.
        var logPath = canonicalLogClaimed ? canonicalLogPath : processLogPath;
        var keepFailure = KeepPreviousLog(logPath);

        var openFailure = TryOpen(logPath, openSink);
        if (openFailure == null) return new OpenedLogFile(logPath, logPath, keepFailure, null);
        if (!canonicalLogClaimed) return new OpenedLogFile(null, logPath, keepFailure, openFailure);

        // A program holding the log open (an editor, a backup tool) makes the sink throw, so the process-id log takes over.
        var openedPath = TryOpen(processLogPath, openSink) == null ? processLogPath : null;
        return new OpenedLogFile(openedPath, logPath, keepFailure, openFailure);
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
        catch (Exception exception) when (IsFileUnavailable(exception))
        {
            return false;
        }
    }

    // Keeps only the newest process-id logs, which pile up because each dual-client run adds one under a fresh pid.
    private void PruneProcessLogs()
    {
        string[] processLogs;
        try
        {
            processLogs = Directory.GetFiles(directory, processLogPattern);
        }
        catch (Exception exception) when (IsFileUnavailable(exception))
        {
            // Best effort, housekeeping never stops start-up.
            return;
        }

        // The extension filter skips .log.lock sidecars, which .NET also matches for a 3-character pattern extension.
        var staleLogs = processLogs
            .Where(file => Path.GetExtension(file).Equals(".log", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Skip(KeptProcessLogs);
        foreach (var file in staleLogs)
        {
            try { File.Delete(file); }
            catch (Exception exception) when (IsFileUnavailable(exception)) { /* still open by a live instance */ }
        }
    }

    // Moves the last session's log to its .prev.log path, replacing an older one, and returns the failure or null.
    private static Exception KeepPreviousLog(string logPath)
    {
        if (!File.Exists(logPath)) return null;

        var previousLogPath = Path.ChangeExtension(logPath, ".prev.log");
        try
        {
            // Delete then move rather than File.Replace, which fails while a reader such as tail -f holds the log.
            File.Delete(previousLogPath);
            File.Move(logPath, previousLogPath);
            return null;
        }
        catch (Exception exception) when (IsFileUnavailable(exception))
        {
            return exception;
        }
    }

    // Clears the path and opens a sink on it, returning the failure or null.
    private static Exception TryOpen(string path, Action<string> openSink)
    {
        try
        {
            // A file left at the path, such as an earlier same-id process's log, would otherwise be appended to.
            File.Delete(path);
        }
        catch (Exception exception) when (IsFileUnavailable(exception))
        {
            // Best effort, a file that can't be deleted is appended to, or fails the open below.
        }

        try
        {
            openSink(path);
            return null;
        }
        catch (Exception exception) when (IsFileUnavailable(exception))
        {
            return exception;
        }
    }

    // A file another program holds, or one this process may not touch, rather than a bug in the caller.
    private static bool IsFileUnavailable(Exception exception) => exception is IOException or UnauthorizedAccessException;
}
