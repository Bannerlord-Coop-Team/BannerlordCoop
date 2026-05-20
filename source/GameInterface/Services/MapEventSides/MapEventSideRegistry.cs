using Common.Logging;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
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
internal class MapEventSideRegistry : AutoRegistryBase<MapEventSide>
{
    public override bool Debug => true;
    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(MapEventSide), new Type[]
        {
            typeof(MapEvent), 
            typeof(BattleSideEnum), 
            typeof(PartyBase)
        })
    };

    public override IEnumerable<MethodBase> DestroyMethods => new MethodBase[] { 
        AccessTools.Method(typeof(MapEventSide), nameof(MapEventSide.HandleMapEventEnd))
    };

    public MapEventSideRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override void RegisterAllObjects()
    {
        foreach (MapEvent mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            int counter = 1;

            foreach (var side in mapEvent._sides.Where(side => side != null))
            {
                var networkId = mapEvent.StringId + "_" + counter++;

                RegisterExistingObject(networkId, side);
            }
        }
    }

    public override void OnClientCreated(MapEventSide obj, string id)
    {
        AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._battleParties))
            .SetValue(obj, new MBList<MapEventParty>());
    }

    public override void OnClientDestroyed(MapEventSide obj, string id)
    {
    }

    public override void OnServerCreated(MapEventSide obj, string id)
    {
    }

    public override void OnServerDestroyed(MapEventSide obj, string id)
    {
    }
}