using Serilog.Core;
using Serilog.Events;
using System;

namespace Common.Logging.Enrichers;

class NetworkEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var networkType = ModInformation.IsServer ? "Server" : "Client";
        var logProperty = propertyFactory.CreateProperty("InstanceType", networkType);

        logEvent.AddPropertyIfAbsent(logProperty);
    }
}
