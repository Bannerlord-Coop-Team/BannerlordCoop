using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents
{
    internal class CaravanPartyComponentSync : IAutoSync
    {
        public CaravanPartyComponentSync(AutoSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent._leader)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent._isElite)));
        }
    }
}