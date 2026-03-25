using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters
{
    class ItemRosterSync : IDynamicSync
    {
        public ItemRosterSync(DynamicSyncRegistry autoSyncBuilder)
        {
            // Re-enable this when we sync the actual ItemRoster data. Currently this is event synced.

            //autoSyncBuilder.AddField(AccessTools.Field(typeof(ItemRoster), nameof(ItemRoster._count)));
        }
    }
}