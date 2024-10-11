using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops
{
    public class WorkshopSync : IAutoSync
    {
        public WorkshopSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Workshop), nameof(Workshop._owner)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Workshop), nameof(Workshop._customName)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Workshop), nameof(Workshop._settlement)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Workshop), nameof(Workshop._tag)));
        }
    }
}
