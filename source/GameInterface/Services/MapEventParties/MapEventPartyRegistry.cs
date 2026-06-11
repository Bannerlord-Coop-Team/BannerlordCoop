using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Patches;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEventParties;
internal class MapEventPartyRegistry : AutoRegistryBase<MapEventParty>
{
    public override bool Debug => true;
    public MapEventPartyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(MapEventParty));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (MapEvent mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            int counter = 1;
            foreach (var side in mapEvent._sides.Where(x => x != null))
            {
                foreach (var party in side.Parties.Where(x => x != null))
                {
                    RegisterExistingObject($"{mapEvent.StringId}_{counter++}", party);
                }
            }
        }
    }

    public override void OnClientCreated(MapEventParty obj, string id)
    {
        using (new AllowedThread())
        {
            obj._woundedInBattle = new TroopRoster();
            obj._diedInBattle = new TroopRoster();
            obj._routedInBattle = new TroopRoster();
            obj._roster = new FlattenedTroopRoster();
        }
    }

    public override void OnClientDestroyed(MapEventParty obj, string id)
    {
    }

    public override void OnServerCreated(MapEventParty obj, string id)
    {
    }

    public override void OnServerDestroyed(MapEventParty obj, string id)
    {
    }
}
