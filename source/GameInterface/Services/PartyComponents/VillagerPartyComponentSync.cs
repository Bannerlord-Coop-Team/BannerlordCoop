using GameInterface.AutoSync;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents
{
    internal class VillagerPartyComponentSync : IAutoSync
    {
        public VillagerPartyComponentSync(AutoSyncRegistry autoSyncBuilder) 
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(VillagerPartyComponent), nameof(VillagerPartyComponent.Village)));
        }
    }
}