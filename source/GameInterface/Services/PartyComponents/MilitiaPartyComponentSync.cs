using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents;
internal class MilitiaPartyComponentSync : IAutoSync
{
    public MilitiaPartyComponentSync(IAutoSyncBuilder autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.Settlement)));
    }
}
