using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Common.Logging;

/// <summary>
/// Counts Warning, Error and Fatal events for the whole session, so the log can end with one
/// summary line saying how the session ended and which sources failed.
/// </summary>
public sealed class SessionSummarySink : ILogEventSink
{
    private const string UnknownSource = "unknown";

    // Emit runs on whatever thread logged; the totals are interlocked and only errors take the lock.
    private readonly object gate = new object();
    private readonly Dictionary<string, int> errorsBySource = new Dictionary<string, int>(StringComparer.Ordinal);
    private int warnings;
    private int errors;
    private int fatal;
    private int written;

    public void Emit(LogEvent logEvent)
    {
        switch (logEvent.Level)
        {
            case LogEventLevel.Warning:
                Interlocked.Increment(ref warnings);
                break;
            case LogEventLevel.Error:
                Interlocked.Increment(ref errors);
                CountSource(logEvent);
                break;
            case LogEventLevel.Fatal:
                Interlocked.Increment(ref fatal);
                CountSource(logEvent);
                break;
        }
    }

    /// <summary>
    /// Writes the session summary once; later calls (an unload after a crash) write nothing.
    /// </summary>
    /// <returns>true when this call wrote the summary.</returns>
    public bool WriteSummary(ILogger logger, string reason)
    {
        if (Interlocked.Exchange(ref written, 1) == 1) return false;

        // Snapshot before writing so the summary lines can never count themselves.
        int warningCount = Volatile.Read(ref warnings);
        int errorCount = Volatile.Read(ref errors);
        int fatalCount = Volatile.Read(ref fatal);
        KeyValuePair<string, int>[] sources;
        lock (gate)
        {
            sources = errorsBySource
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .ToArray();
        }

        logger.Information("[Session] summary reason={Reason:l} warnings={Warnings} errors={Errors} fatal={Fatal}",
            reason, warningCount, errorCount, fatalCount);

        foreach (var source in sources)
        {
            logger.Information("[Session] errors source={Source:l} n={Count}", source.Key, source.Value);
        }

        return true;
    }

    // Fatal events count toward their source too, so a crash names where it came from.
    private void CountSource(LogEvent logEvent)
    {
        var source = logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var value)
            && value is ScalarValue { Value: string context }
            ? context
            : UnknownSource;

        lock (gate)
        {
            errorsBySource.TryGetValue(source, out var count);
            errorsBySource[source] = count + 1;
        }
    }
}
