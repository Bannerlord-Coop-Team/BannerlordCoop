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
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.CanBeClaimed)));
        autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(SettlementClaimantCampaignBehavior), nameof(SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimedBy)));
        autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_player_ask_to_claim_land_answer_on_consequence)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimValue)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Culture)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.HasVisited)));
        autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.OnSettlementEntered)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Hideout)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.LastVisitTimeOfOwner)));
        autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyInternal)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.MilitiaPartyComponent)));
        autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnInitialize)));
        autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnFinalize)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.NumberOfLordPartiesTargeting)));
        autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(KingdomManager), nameof(KingdomManager.UpdateLordPartyVariablesRelatedToSettlements)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Stash)));
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
    }
}
