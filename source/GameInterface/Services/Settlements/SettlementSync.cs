using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements;
internal class SettlementSync : IAutoSync
{
    public SettlementSync(IAutoSyncBuilder autoSyncBuilder)
    {
        var ownerClanProp = AccessTools.Property(typeof(Settlement), nameof(Settlement.OwnerClan));
        if (ownerClanProp != null && ownerClanProp.CanWrite)
            autoSyncBuilder.AddProperty(ownerClanProp);
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.LastAttackerParty)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.CurrentSiegeState)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.BribePaid)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), "LastVisitTimeOfOwner"));
    }
}
