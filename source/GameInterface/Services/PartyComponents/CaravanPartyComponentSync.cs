using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents
{
    internal class CaravanPartyComponentSync : IDynamicSync
    {
        public CaravanPartyComponentSync(DynamicSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.Settlement)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.Owner)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent._leader)));
        }
    }
}