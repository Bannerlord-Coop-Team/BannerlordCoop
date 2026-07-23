using Common.Util;
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

namespace GameInterface.Services.TroopUpgradeTrackers;
internal class TroopUpgradeTrackerRegistry : AutoRegistryBase<TroopUpgradeTracker>
{
    public TroopUpgradeTrackerRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(TroopUpgradeTracker), new Type[0] )
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();
    public override void RegisterAllObjects()
    {
        foreach (var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (mapEvent.StringId == null) continue;
            if (mapEvent.TroopUpgradeTracker == null) continue;

            RegisterExistingObject(mapEvent.StringId, mapEvent.TroopUpgradeTracker);
        }
    }

    public override void OnClientCreated(TroopUpgradeTracker obj, string id)
    {
        using (new AllowedThread())
        {
            obj._upgradedRegulars = new Dictionary<Tuple<PartyBase, CharacterObject>, int>();
            obj._mapEventParties = new List<MapEventParty>();
            obj._heroSkills = new Dictionary<Hero, int[]>();
        }
    }

    public override void OnClientDestroyed(TroopUpgradeTracker obj, string id)
    {
    }

    public override void OnServerCreated(TroopUpgradeTracker obj, string id)
    {
    }

    public override void OnServerDestroyed(TroopUpgradeTracker obj, string id)
    {
    }
}
