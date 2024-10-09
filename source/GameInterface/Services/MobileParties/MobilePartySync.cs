using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;
internal class MobilePartySync : IAutoSync
{
    public MobilePartySync(IAutoSyncBuilder autoSyncBuilder)
    {
        // Sync a property
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Ai)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party)));
    }
}
