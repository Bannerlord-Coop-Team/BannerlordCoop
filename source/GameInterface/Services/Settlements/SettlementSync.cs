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
    public SettlementSync(DynamicSyncRegistry dynamicSyncRegistry)
    {
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.CanBeClaimed)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimedBy)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimValue)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Culture)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.HasVisited)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Hideout)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.LastVisitTimeOfOwner)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.MilitiaPartyComponent)));

        // readonly
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Stash)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Town)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Village)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._isVisible)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._lastAttackerParty)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._name)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._nextLocatable)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._numberOfLordPartiesAt)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.NumberOfLordPartiesTargeting)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._position)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._readyMilitia)));

        // Unsure if these need to be added
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._settlementWallSectionHitPointsRatioList)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._boundVillages)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._lastAttackerParty)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._siegeEngineMissiles)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.GatePosition)));

        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(SettlementClaimantCampaignBehavior), nameof(SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(KingdomManager), nameof(KingdomManager.UpdateLordPartyVariablesRelatedToSettlements)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnInitialize)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnFinalize)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyInternal)));
        //dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(LordConversationsCampaignBehavior), nameof(LordConversationsCampaignBehavior.conversation_player_ask_to_claim_land_answer_on_consequence)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.OnSettlementEntered)));


    }
}
