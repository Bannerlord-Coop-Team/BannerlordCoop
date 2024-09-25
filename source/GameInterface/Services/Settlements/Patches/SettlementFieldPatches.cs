using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches
{
    internal class SettlementFieldPatches : IAutoSync
    {
        public SettlementFieldPatches(IAutoSyncBuilder autoSyncBuilder)
        {
            //CanBeClaimed
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.CanBeClaimed)));
            autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(SettlementClaimantCampaignBehavior), nameof(SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged)));

            //ClaimedBy
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimedBy)));
            autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_player_ask_to_claim_land_answer_on_consequence)));

            //ClaimValue
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimValue)));

            //Culture
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Culture)));

            //HasVisited
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.HasVisited)));
            autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.OnSettlementEntered)));

            //Hideout
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Hideout)));

            //LastVisitTimeOfOwner
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.LastVisitTimeOfOwner)));
            autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyInternal)));

            //MilitiaPartyComponent
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.MilitiaPartyComponent)));
            autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnInitialize)));
            autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnFinalize)));

            //NumberOfLordPartiesTargeting
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.NumberOfLordPartiesTargeting)));
            autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(KingdomManager), nameof(KingdomManager.UpdateLordPartyVariablesRelatedToSettlements)));

            //Stash
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Stash)));

            //Town
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Town)));

            //Village
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Village)));

            //_gatePosition
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._gatePosition)));

            //_isVisible
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._isVisible)));

            //_lastAttackerParty
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._lastAttackerParty)));

            //_locatorNodeIndex
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._locatorNodeIndex)));

            //_name
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._name)));

            //_nextLocatable
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._nextLocatable)));

            //_numberOfLordsAt
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._numberOfLordPartiesAt)));

            //_position
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._position)));

            //_readyMilita
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._readyMilitia)));
        }
    }
}
