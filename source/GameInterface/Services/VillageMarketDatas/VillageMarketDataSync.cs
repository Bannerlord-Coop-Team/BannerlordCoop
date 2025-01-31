using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageMarketDatas
{
    internal class VillageMarketDataSync : IAutoSync
    {
        public VillageMarketDataSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(VillageMarketData), nameof(VillageMarketData._village)));
        }
    }
}
