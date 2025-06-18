using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents
{
    internal class GarrisonPartyComponentSync : IDynamicSync
    {
        public GarrisonPartyComponentSync(DynamicSyncRegistry autoSyncBuilder) 
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(GarrisonPartyComponent), nameof(GarrisonPartyComponent.Settlement)));
        }
    }
}
