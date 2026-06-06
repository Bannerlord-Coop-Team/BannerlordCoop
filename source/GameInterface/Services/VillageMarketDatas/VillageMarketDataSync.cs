using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageMarketDatas
{
    internal class VillageMarketDataSync : IDynamicSync
    {
        public VillageMarketDataSync(DynamicSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(VillageMarketData), nameof(VillageMarketData._village)));
        }
    }
}
