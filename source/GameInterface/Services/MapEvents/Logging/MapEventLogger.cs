using Common;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Logging;

internal interface IMapEventLogger
{
    void DebugMapEvent(MapEvent mapEvent, string messageTemplate, params object[] propertyValues);
    void DebugMapEventId(string mapEventId, string messageTemplate, params object[] propertyValues);

    void ErrorMapEvent(Exception ex, MapEvent mapEvent, string messageTemplate, params object[] propertyValues);
    void ErrorMapEventId(Exception ex, string mapEventId, string messageTemplate, params object[] propertyValues);
}

internal sealed class MapEventLogger : IMapEventLogger
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventLogger>();

    private readonly IObjectManager objectManager;

    public MapEventLogger(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void DebugMapEvent(MapEvent mapEvent, string messageTemplate, params object[] propertyValues)
    {
        if (!MapEventConfig.Debug) return;

        if (!objectManager.TryGetIdWithLogging(mapEvent, out string mapEventId)) return;

        DebugMapEventId(mapEventId, messageTemplate, propertyValues);
    }

    public void DebugMapEventId(string mapEventId, string messageTemplate, params object[] propertyValues)
    {
        if (!MapEventConfig.Debug) return;

        Logger
            .ForContext("InstanceType", ModInformation.IsServer ? "Server" : "Client")
            .ForContext("LogGroup", "MapEvent")
            .ForContext("MapEventId", mapEventId)
            .Debug(messageTemplate, propertyValues ?? Array.Empty<object>());
    }

    public void ErrorMapEvent(Exception ex, MapEvent mapEvent, string messageTemplate, params object[] propertyValues)
    {
        if (!objectManager.TryGetIdWithLogging(mapEvent, out string mapEventId)) return;

        ErrorMapEventId(ex, mapEventId, messageTemplate, propertyValues);
    }

    public void ErrorMapEventId(Exception ex, string mapEventId, string messageTemplate, params object[] propertyValues)
    {
        Logger
            .ForContext("InstanceType", ModInformation.IsServer ? "Server" : "Client")
            .ForContext("LogGroup", "MapEvent")
            .ForContext("MapEventId", mapEventId)
            .Error(ex, messageTemplate, propertyValues ?? Array.Empty<object>());
    }
}