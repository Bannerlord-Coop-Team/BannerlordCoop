using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;
internal class MobilePartySync : IAutoSync
{
    public MobilePartySync(IAutoSyncBuilder autoSyncBuilder)
    {
        // Sync Properties
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Ai)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ActualClan)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.LastVisitedSettlement)));

        // Sync Fields
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._currentSettlement)));
    }
}
