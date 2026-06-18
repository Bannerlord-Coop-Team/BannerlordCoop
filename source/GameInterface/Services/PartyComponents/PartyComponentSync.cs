using GameInterface.AutoSync;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents;
internal class PartyComponentSync : IAutoSync
{
    public PartyComponentSync(AutoSyncRegistry autoSyncBuilder)
    {
        // Not working always detected as same???
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyComponent), nameof(PartyComponent.MobileParty)), debug: true);
    }
}
