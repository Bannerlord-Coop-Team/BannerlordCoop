using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.TownMarketDatas
{
    internal class TownMarketDataSync : IDynamicSync
    {
        public TownMarketDataSync(DynamicSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(TownMarketData), nameof(TownMarketData._town)));
        }
    }
}
