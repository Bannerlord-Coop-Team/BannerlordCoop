using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents;
internal class PartyComponentSync : IAutoSync
{
    public PartyComponentSync(IAutoSyncBuilder autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyComponent), nameof(PartyComponent.MobileParty)));
    }
}
