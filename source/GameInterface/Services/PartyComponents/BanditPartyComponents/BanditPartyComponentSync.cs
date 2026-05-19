using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.BanditPartyComponents
{
    internal class BanditPartyComponentSync : IDynamicSync
    {
        public BanditPartyComponentSync(DynamicSyncRegistry autoSyncBuilder)
        {
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.Settlement)));

            //autoSyncBuilder.AddField(AccessTools.Field(typeof(BanditPartyComponent), nameof(BanditPartyComponent.Clan)));
        }
    }
}