using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters;

internal class TroopRosterSync : IDynamicSync
{
    public TroopRosterSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(TroopRoster), nameof(TroopRoster.OwnerParty)));
        
        autoSyncBuilder.AddField(AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._troopRosterElementsVersion)));
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._count)));
    }
}