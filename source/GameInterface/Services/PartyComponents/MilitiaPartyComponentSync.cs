using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents;
internal class MilitiaPartyComponentSync : IDynamicSync
{
    public MilitiaPartyComponentSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.Settlement)));
    }
}
