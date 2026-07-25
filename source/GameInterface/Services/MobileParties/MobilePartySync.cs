using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;
internal class MobilePartySync : IAutoSync
{
    public MobilePartySync(AutoSyncRegistry autoSyncBuilder)
    {
        // Sync Fields
        autoSyncBuilder.AddTargetMethod(typeof(MobileParty), AccessTools.Method(typeof(DefaultClanFinanceModel), nameof(DefaultClanFinanceModel.ApplyMoraleEffect)), GameInterface.HARMONY_GAME_STARTED_CATEGORY);
        autoSyncBuilder.AddTargetMethod(typeof(MobileParty), AccessTools.Method(typeof(MobilePartyAi), nameof(MobilePartyAi.GetFleeBehavior)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty.HasUnpaidWages)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._disorganizedUntilTime)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._doNotAttackMainParty)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._customHomeSettlement)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._isDisorganized)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._isCurrentlyUsedByAQuest)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._ignoredUntilTime)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._besiegerCampResetStarted)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MobileParty), nameof(MobileParty._partyComponent)));

        // Movement is replicated atomically by NetworkUpdatePartyBehavior. These members are
        // interdependent, so independent AutoSync messages can apply a behavior before its target.

        // Sync Properties
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Ai)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.CurrentSettlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ActualClan)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.LastVisitedSettlement)));
        //new
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Aggressiveness)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Objective)));
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Army)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsActive)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsPartyTradeActive)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.PartyTradeGold)));
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.PartyTradeTaxGold)));
        ////StationaryStartTime called on tick so lags out, disregard?
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ShouldJoinPlayerBattles)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsDisbanding)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.AttachedTo)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.BesiegerCamp)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Scout)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Engineer)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Quartermaster)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Surgeon)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.FirstMate)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Navigator)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.PartyTradeTaxGold)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.RecentEventsMorale)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MobileParty), nameof(MobileParty.EventPositionAdder)));
    }
}
