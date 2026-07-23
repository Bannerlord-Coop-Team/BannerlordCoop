using Serilog.Core;
using Serilog.Events;
using System;

namespace Common.Logging.Enrichers;

class StackTraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Level < LogEventLevel.Error) return;

        var stacktraceProperty = propertyFactory.CreateProperty("StackTrace", Environment.StackTrace);
        logEvent.AddPropertyIfAbsent(stacktraceProperty);
    }
}
