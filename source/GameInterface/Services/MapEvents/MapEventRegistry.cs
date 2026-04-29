using Common;
using Common.Logging;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Registry for <see cref="MapEvent"/> objects
/// </summary>
internal class MapEventRegistry : IAutoRegistry<MapEvent>
{
    public MapEventRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    private static ILogger Logger = LogManager.GetLogger<MapEvent>();

    public IEnumerable<MethodBase> Constructors => new MethodBase[] { AccessTools.Constructor(typeof(MapEvent)) };

    public IEnumerable<MethodBase> DestroyMethods => new MethodBase[] { };

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            int counter = 1;

            var networkId = nameof(mapEvent) + "_" + mapEvent.StringId + "_" + counter++;

            if (!objectManager.AddExisting(networkId, mapEvent))
            {
                Logger.Error("Unable to register {type}", typeof(MapEvent));
                continue;
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