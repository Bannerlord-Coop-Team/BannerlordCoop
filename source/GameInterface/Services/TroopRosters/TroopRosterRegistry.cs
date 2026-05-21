using Common;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Patches;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;

namespace GameInterface.Services.TroopRosters;
internal class TroopRosterRegistry : AutoRegistryBase<TroopRoster>
{
    public override bool Debug => true;
    public TroopRosterRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(TroopRoster));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var parties = Campaign.Current?.MobileParties;
        if (parties == null)
        {
            Logger.Error("Unable to register {Type} when Campaign.MobileParties is null", nameof(TroopRoster));
            return;
        }

        foreach (MobileParty party in parties)
        {

            if (party == null) continue;

            if (party.MemberRoster is null)
            {
                Logger.Error("Unable to register {Roster} for party {PartyId}: roster is null", nameof(MobileParty.MemberRoster), party.StringId);
                continue;
            }

            RegisterExistingObject($"{nameof(MobileParty.MemberRoster)}_{party.StringId}", party.MemberRoster);

            if (party.PrisonRoster is null)
            {
                Logger.Error("Unable to register {Roster} for party {PartyId}: roster is null", nameof(MobileParty.PrisonRoster), party.StringId);
                continue;
            }

            RegisterExistingObject($"{nameof(MobileParty.PrisonRoster)}_{party.StringId}", party.PrisonRoster);
        }
    }

    public override void OnClientCreated(TroopRoster obj, string id)
    {
        obj.data = new TroopRosterElement[4];
        obj._count = 0;
        obj._troopRosterElements = new MBList<TroopRosterElement>();
        obj.InitializeCachedData();
    }

    public override void OnClientDestroyed(TroopRoster obj, string id)
    {
    }

    public override void OnServerCreated(TroopRoster obj, string id)
    {
    }

    public override void OnServerDestroyed(TroopRoster obj, string id)
    {
    }
}
