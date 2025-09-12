using Common;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters;
internal class TroopRosterRegistry : IAutoRegistry<TroopRoster>
{
    ILogger Logger { get; }
    public TroopRosterRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(TroopRoster))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<TroopRoster> registry)
    {
        foreach (MobileParty party in Campaign.Current.MobileParties)
        {
            if (registry.RegisterExistingObject($"{nameof(MobileParty.MemberRoster)}_{party.StringId}", party.MemberRoster) == false)
                Logger.Error($"Unable to register {nameof(MobileParty.MemberRoster)}");
            if (registry.RegisterExistingObject($"{nameof(MobileParty.PrisonRoster)}_{party.StringId}", party.PrisonRoster) == false)
                Logger.Error($"Unable to register {nameof(MobileParty.PrisonRoster)}");
        }
    }

    public void OnClientCreated(TroopRoster obj, string id)
    {
        ;
    }

    public void OnClientDestroyed(TroopRoster obj, string id)
    {
    }

    public void OnServerCreated(TroopRoster obj, string id)
    {
        ;
    }

    public void OnServerDestroyed(TroopRoster obj, string id)
    {
    }
}
