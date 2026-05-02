using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Registry for <see cref="MapEvent"/> objects
/// </summary>
internal class MapEventRegistry : IAutoRegistry<MapEvent>
{
    ILogger Logger { get; }
    public MapEventRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        int counter = 1;
        foreach (var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (mapEvent.StringId == null) return;

            var networkId = mapEvent.StringId + "_" + counter++;

            if (!objectManager.AddExisting(networkId, mapEvent))
            {
                Logger.Error($"Unable to register {mapEvent}");
            }
        }
    }

    public void OnClientCreated(MapEvent obj, string id)
    {
    }

    public void OnClientDestroyed(MapEvent obj, string id)
    {
    }

    public void OnServerCreated(MapEvent obj, string id)
    {
    }

    public void OnServerDestroyed(MapEvent obj, string id)
    {
    }
}
