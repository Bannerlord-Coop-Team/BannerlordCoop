using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents
{
    internal class VillagerPartyComponentSync : IDynamicSync
    {
        public VillagerPartyComponentSync(DynamicSyncRegistry autoSyncBuilder) 
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(VillagerPartyComponent), nameof(VillagerPartyComponent.Village)));
        }
    }
}