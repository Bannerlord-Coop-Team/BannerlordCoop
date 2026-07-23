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

        var values = BuildPropertyValues(mapEventId, propertyValues);

        Logger.Debug(
            "[{InstanceType}][MapEvent:{MapEventId}] " + messageTemplate,
            values);
    }

    public void ErrorMapEvent(Exception ex, MapEvent mapEvent, string messageTemplate, params object[] propertyValues)
    {
        if (!objectManager.TryGetIdWithLogging(mapEvent, out string mapEventId)) return;

        ErrorMapEventId(ex, mapEventId, messageTemplate, propertyValues);
    }

    public void ErrorMapEventId(Exception ex, string mapEventId, string messageTemplate, params object[] propertyValues)
    {
        var values = BuildPropertyValues(mapEventId, propertyValues);

        Logger.Error(
            ex,
            "[{InstanceType}][MapEvent:{MapEventId}] " + messageTemplate,
            values);
    }

    private static object[] BuildPropertyValues(string mapEventId, object[] propertyValues)
    {
        propertyValues ??= Array.Empty<object>();

        var values = new object[propertyValues.Length + 2];
        values[0] = ModInformation.IsServer ? "Server" : "Client";
        values[1] = mapEventId;

        Array.Copy(propertyValues, 0, values, 2, propertyValues.Length);

        return values;
    }
}