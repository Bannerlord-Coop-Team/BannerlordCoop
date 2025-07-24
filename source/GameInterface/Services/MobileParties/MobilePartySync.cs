using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;
internal class MobilePartySync : IDynamicSync
{
    public MobilePartySync(DynamicSyncRegistry autoSyncBuilder)
    {
        // Sync Fields
        autoSyncBuilder.AddTargetMethod(typeof(MobileParty), AccessTools.Method(typeof(DefaultClanFinanceModel), nameof(DefaultClanFinanceModel.ApplyMoraleEffect)));
        autoSyncBuilder.AddTargetMethod(typeof(MobileParty), AccessTools.Method(typeof(MobilePartyAi), nameof(MobilePartyAi.GetFleeBehavior)));

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
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._ignoredUntilTime)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._besiegerCampResetStarted)));

        // Sync Properties
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Ai)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.CurrentSettlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ActualClan)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.LastVisitedSettlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.TargetParty)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.TargetSettlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.TargetPosition)));
        //new
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.CustomName)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Aggressiveness)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Objective)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Army)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsActive)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsPartyTradeActive)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.PartyTradeGold)));
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.PartyTradeTaxGold)));
        ////StationaryStartTime called on tick so lags out, disregard?
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.VersionNo)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ShouldJoinPlayerBattles)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsDisbanding)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.AttachedTo)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.BesiegerCamp)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Scout)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Engineer)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Quartermaster)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Surgeon)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.PartyTradeTaxGold)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.RecentEventsMorale)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.EventPositionAdder)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.PartyComponent)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsMilitia)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsLordParty)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsVillager)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsCaravan)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsGarrison)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsCustomParty)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsBandit)));
    }
}
