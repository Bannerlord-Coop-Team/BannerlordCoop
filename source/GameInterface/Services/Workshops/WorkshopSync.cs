using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops
{
    public class WorkshopSync : IDynamicSync
    {
        public WorkshopSync(DynamicSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Workshop), nameof(Workshop._owner)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Workshop), nameof(Workshop._customName)));
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(Workshop), nameof(Workshop._settlement)));
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(Workshop), nameof(Workshop._tag)));
        }
    }
}
