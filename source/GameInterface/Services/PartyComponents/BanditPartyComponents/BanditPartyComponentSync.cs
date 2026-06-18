using GameInterface.AutoSync;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.BanditPartyComponents
{
    internal class BanditPartyComponentSync : IAutoSync
    {
        public BanditPartyComponentSync(AutoSyncRegistry autoSyncBuilder)
        {
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.Settlement)));

            //autoSyncBuilder.AddField(AccessTools.Field(typeof(BanditPartyComponent), nameof(BanditPartyComponent.Clan)));
        }
    }
}