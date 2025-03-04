using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
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
    public TroopRosterSync(IAutoSyncBuilder autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(TroopRoster), nameof(TroopRoster.OwnerParty)));
        
        autoSyncBuilder.AddField(AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._troopRosterElementsVersion)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._count)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._isPrisonRoster)));
    }
}
