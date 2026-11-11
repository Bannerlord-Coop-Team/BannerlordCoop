using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Logging;

/// <summary>
/// Counts Warning, Error and Fatal events for the whole session, so the log can end with one summary line.
/// </summary>
public interface ISessionSummarySink : ILogEventSink
{
    /// <summary>Writes the summary once and returns true; later calls (an unload after a crash) write nothing.</summary>
    bool WriteSummary(ILogger logger, string reason);
}

/// <inheritdoc />
public sealed class SessionSummarySink : ISessionSummarySink
{
    private const string UnknownSource = "unknown";

    private readonly object gate = new object();
    private readonly Dictionary<string, int> errorsBySource = new Dictionary<string, int>(StringComparer.Ordinal);
    private int warnings;
    private int fatal;
    private bool written;

    /// <summary>Counts the event when it is a Warning, Error or Fatal.</summary>
    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < LogEventLevel.Warning) return;

        lock (gate)
        {
            switch (logEvent.Level)
            {
                case LogEventLevel.Warning:
                    warnings++;
                    break;
                case LogEventLevel.Error:
                    var source = GetSource(logEvent);
                    errorsBySource.TryGetValue(source, out var count);
                    errorsBySource[source] = count + 1;
                    break;
                case LogEventLevel.Fatal:
                    fatal++;
                    break;
            }
        }
    }

    /// <inheritdoc />
    public bool WriteSummary(ILogger logger, string reason)
    {
        int warningCount;
        int fatalCount;
        KeyValuePair<string, int>[] sources;
        lock (gate)
        {
            if (written) return false;

            written = true;
            warningCount = warnings;
            fatalCount = fatal;
            sources = errorsBySource
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .ToArray();
        }

        logger.Information("[Session] summary reason={Reason:l} warnings={Warnings} errors={Errors} fatal={Fatal}",
            reason, warningCount, sources.Sum(source => source.Value), fatalCount);

        foreach (var source in sources)
        {
            // Closed generic type names hold spaces, so they're replaced to keep each key=value pair space-free.
            logger.Information("[Session] errors source={Source:l} n={Count}", source.Key.Replace(' ', '_'), source.Value);
        }

        return true;
    }

    // Returns the event's SourceContext, or "unknown" when it has none.
    private static string GetSource(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var value)) return UnknownSource;
        if (value is not ScalarValue { Value: string source }) return UnknownSource;

        return source;
    }
}
