using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.TownMarketDatas
{
    internal class TownMarketDataSync : IAutoSync
    {
        public TownMarketDataSync(AutoSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(TownMarketData), nameof(TownMarketData._town)));
            // Dictionary<ItemCategory, ItemData>: keys sync by reference, values via ItemDataSurrogate
            autoSyncBuilder.AddField(AccessTools.Field(typeof(TownMarketData), nameof(TownMarketData._itemDict)));
        }
    }
}
