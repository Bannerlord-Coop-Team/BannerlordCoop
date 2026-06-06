using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents;
internal class PartyComponentSync : IDynamicSync
{
    public PartyComponentSync(DynamicSyncRegistry autoSyncBuilder)
    {
        // Not working always detected as same???
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyComponent), nameof(PartyComponent.MobileParty)), debug: true);
    }
}
