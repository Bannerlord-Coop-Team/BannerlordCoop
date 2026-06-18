using GameInterface.AutoSync;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters
{
    class ItemRosterSync : IAutoSync
    {
        public ItemRosterSync(AutoSyncRegistry autoSyncBuilder)
        {
            // Re-enable this when we sync the actual ItemRoster data. Currently this is event synced.

            //autoSyncBuilder.AddField(AccessTools.Field(typeof(ItemRoster), nameof(ItemRoster._count)));
        }
    }
}