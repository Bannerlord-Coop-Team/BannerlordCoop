using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages
{
    internal class VillageFieldSync : IAutoSync
    {
        public VillageFieldSync(IAutoSyncBuilder autoSyncBuilder) 
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Village), nameof(Village.VillagerPartyComponent)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Village), nameof(Village.VillageType)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Village), nameof(Village._bound)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Village), nameof(Village._marketData)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Village), nameof(Village._tradeBound)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Village), nameof(Village._villageState)));
        }
    }
}
