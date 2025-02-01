using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents
{
    internal class GarrisonPartyComponentSync : IAutoSync
    {
        public GarrisonPartyComponentSync(IAutoSyncBuilder autoSyncBuilder) 
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(GarrisonPartyComponent), nameof(GarrisonPartyComponent.Settlement)));
        }
    }
}
