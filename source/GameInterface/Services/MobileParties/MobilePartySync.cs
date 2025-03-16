using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;
internal class MobilePartySync : IAutoSync
{
    public MobilePartySync(IAutoSyncBuilder autoSyncBuilder)
    {
        // Sync Fields
        autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(DefaultClanFinanceModel), nameof(DefaultClanFinanceModel.ApplyMoraleEffect)));
        autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(MobilePartyAi), nameof(MobilePartyAi.GetFleeBehavior)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._attachedTo)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty.HasUnpaidWages)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._disorganizedUntilTime)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._partySizeRatioLastCheckVersion)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._latestUsedPaymentRatio)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._cachedPartySizeRatio)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._cachedPartySizeLimit)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._doNotAttackMainParty)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._customHomeSettlement)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._isDisorganized)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._isCurrentlyUsedByAQuest)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._partyTradeGold)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._ignoredUntilTime)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._besiegerCampResetStarted)));

        // Sync Properties
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Ai)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.CurrentSettlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ActualClan)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.LastVisitedSettlement)));
    }
}
