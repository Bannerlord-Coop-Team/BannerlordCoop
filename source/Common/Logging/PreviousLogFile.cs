using System;
using System.IO;

namespace Common.Logging;

/// <summary>
/// Keeps the last session's log as <c>Coop_&lt;role&gt;.prev.log</c>, so the log of a session that crashed
/// survives one relaunch.
/// </summary>
public static class PreviousLogFile
{
    public static string GetPath(string logPath) => System.IO.Path.ChangeExtension(logPath, ".prev.log");

    // Never throws, because it runs during start-up before logging exists; the caller logs the failure.
    public static bool TryKeep(string logPath, out Exception failure)
    {
        failure = null;
        try
        {
            if (!File.Exists(logPath)) return true;

            var previousLogPath = GetPath(logPath);

            File.Delete(previousLogPath);
            File.Move(logPath, previousLogPath);

            return true;
        }
        catch (Exception exception)
        {
            failure = exception;
            return false;
        }
    }
}
