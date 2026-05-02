using Common;
using Common.Logging;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEventSides;

/// <summary>
/// Registry for <see cref="MapEventSide"/> objects
/// </summary>
internal class MapEventSideRegistry : IAutoRegistry<MapEventSide>
{
    private static ILogger Logger = LogManager.GetLogger<MapEventSide>();

    public IEnumerable<MethodBase> Constructors => new MethodBase[] { };

    public IEnumerable<MethodBase> DestroyMethods => new MethodBase[] { };

    public MapEventSideRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        foreach (MapEvent mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            int counter = 1;

            foreach (var side in mapEvent._sides)
            {
                if (side == null) continue;

                var networkId = mapEvent.StringId + "_" + counter++;

                if (objectManager.AddExisting(networkId, side) == false)
                    Logger.Error("Unable to register MapEventSide {id} in the object manager", side.ToString());
            }
        }
    }

    public void OnClientCreated(MapEventSide obj, string id)
    {
    }

    public void OnClientDestroyed(MapEventSide obj, string id)
    {
    }

    public void OnServerCreated(MapEventSide obj, string id)
    {
    }

    public void OnServerDestroyed(MapEventSide obj, string id)
    {
    }
}