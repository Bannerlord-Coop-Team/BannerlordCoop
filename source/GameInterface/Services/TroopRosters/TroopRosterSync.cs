using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters;

internal class TroopRosterSync : IAutoSync
{
    public TroopRosterSync(AutoSyncRegistry autoSyncBuilder)
    {
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._troopRosterElementsVersion)));
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._count)));
    }
}