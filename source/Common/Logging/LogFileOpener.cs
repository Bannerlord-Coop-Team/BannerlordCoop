using System;
using System.Collections.Generic;
using System.IO;

namespace Common.Logging;

/// <summary>
/// Opens the log at the first path that opens, so a log file another program holds open moves the log to the next name.
/// </summary>
public interface ILogFileOpener
{
    /// <summary>Returns the first sink that opens, or null with the last file failure when none does.</summary>
    TSink OpenFirst<TSink>(IEnumerable<string> paths, Func<string, TSink> open, out string openedPath, out Exception lastFailure)
        where TSink : class;
}

/// <inheritdoc />
public class LogFileOpener : ILogFileOpener
{
    /// <inheritdoc />
    public TSink OpenFirst<TSink>(IEnumerable<string> paths, Func<string, TSink> open, out string openedPath, out Exception lastFailure)
        where TSink : class
    {
        openedPath = null;
        lastFailure = null;

        foreach (var path in paths)
        {
            try
            {
                var sink = open(path);
                openedPath = path;
                return sink;
            }
            // Only a file problem moves on to the next path; any other exception is a bug in the caller.
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                lastFailure = exception;
            }
        }

        return null;
    }
}
