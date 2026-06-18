using GameInterface.AutoSync;
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
        }
    }
}
