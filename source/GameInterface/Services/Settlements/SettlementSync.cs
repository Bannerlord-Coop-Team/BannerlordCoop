using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements;
internal class SettlementSync : IDynamicSync
{
    public SettlementSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.CanBeClaimed)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimedBy)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimValue)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Culture)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.HasVisited)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Hideout)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.LastVisitTimeOfOwner)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.MilitiaPartyComponent)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.NumberOfLordPartiesTargeting)));
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Stash)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Town)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Village)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._gatePosition)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._isVisible)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._lastAttackerParty)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._locatorNodeIndex)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._name)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._nextLocatable)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._numberOfLordPartiesAt)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._position)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._readyMilitia)));

        autoSyncBuilder.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(SettlementClaimantCampaignBehavior), nameof(SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged)));
        autoSyncBuilder.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(KingdomManager), nameof(KingdomManager.UpdateLordPartyVariablesRelatedToSettlements)));
        autoSyncBuilder.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnInitialize)));
        autoSyncBuilder.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnFinalize)));
        autoSyncBuilder.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyInternal)));
        autoSyncBuilder.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_player_ask_to_claim_land_answer_on_consequence)));
        autoSyncBuilder.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.OnSettlementEntered)));


    }
}
