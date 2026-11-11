using System;
using System.IO;

namespace Common.Logging;

/// <summary>
/// Keeps the last session's log as <c>Coop_&lt;role&gt;.prev.log</c>, so the log of a session that crashed survives one relaunch.
/// </summary>
public interface IPreviousLogFile
{
    /// <summary>Returns the <c>.prev.log</c> path that keeps the log at <paramref name="logPath"/>.</summary>
    string GetPath(string logPath);

    /// <summary>Moves the log to its <c>.prev.log</c> path, replacing an older one; never throws.</summary>
    bool TryKeep(string logPath, out Exception failure);
}

/// <inheritdoc />
public class PreviousLogFile : IPreviousLogFile
{
    /// <inheritdoc />
    public string GetPath(string logPath) => System.IO.Path.ChangeExtension(logPath, ".prev.log");

    /// <inheritdoc />
    public bool TryKeep(string logPath, out Exception failure)
    {
        failure = null;

        // Never throws, because it runs during start-up before logging exists; the caller logs the failure.
        try
        {
            if (!File.Exists(logPath)) return true;

            // Delete then move rather than File.Replace, which fails while a reader such as tail -f holds the log.
            File.Delete(GetPath(logPath));
            File.Move(logPath, GetPath(logPath));

            return true;
        }
        catch (Exception exception)
        {
            failure = exception;
            return false;
        }
    }
}
