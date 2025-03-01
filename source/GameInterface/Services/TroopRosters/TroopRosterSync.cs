using GameInterface.AutoSync;
using GameInterface.AutoSync.Registry;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters;

internal class TroopRosterSync : IAutoSync
{
    ILogger Logger { get; }
    public TroopRosterSync(IAutoSyncBuilder autoSyncBuilder, IAutoRegistryFactory autoRegistryFactory, ILogger logger)
    {
        Logger = logger;

        // Call top level first as it sets values
        // If we do not call the top constructor first, those values will not be synced
        var ctors = new MethodBase[] {
            AccessTools.Constructor(typeof(TroopRoster), Array.Empty<Type>())
        };
        autoRegistryFactory.TryRegisterType<TroopRoster>(ctors, RegisterAll);

        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(TroopRoster), nameof(TroopRoster.OwnerParty)));
        
        autoSyncBuilder.AddField(AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._troopRosterElementsVersion)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._count)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._isPrisonRoster)));
    }

    private void RegisterAll(AutoRegistry<TroopRoster> registry)
    {
        foreach (MobileParty party in Campaign.Current.MobileParties)
        {
            if (registry.RegisterNewObject(party.MemberRoster, out var _) == false) Logger.Error($"Unable to register {nameof(MobileParty.MemberRoster)}");
            if (registry.RegisterNewObject(party.PrisonRoster, out var _) == false) Logger.Error($"Unable to register {nameof(MobileParty.PrisonRoster)}");
        }
    }
}
