using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation) for this quest type's turn-in/completion paths - independently verified per the task's explicit
/// "check who can physically reach the antagonist merchant NPC's dialogue" instruction, a real gap the design
/// critique did not spell out.
///
/// <c>DiscussDialogFlow</c> (the delivery dialogue with the QUEST GIVER, wired via <c>SetDialogs()</c> - called
/// unconditionally from the Quest's own CTOR, not just the genuine accepter's <c>QuestAcceptedConsequences</c>)
/// is live on EVERY peer's <c>ConversationManager</c> the instant a mirror Quest object exists, owner and
/// non-owner alike - same "every peer gets a real, locally-registered dialogue for the same logical issue"
/// shape <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment documents for Village Needs
/// Tools. Its two delivery options use ANONYMOUS <c>.Condition(delegate {...})</c> lambdas (no stable named
/// condition method to gate the way <c>PlayerHasTools</c> is), so - same technique
/// <see cref="ArtisanCantSellProductsAtAFairPriceOwnershipGatePatches"/> already uses for its own delivery
/// flow - the actual state-mutating Consequence methods are gated directly: <c>DeliverItemsPartiallyOnConsequence</c>/
/// <c>DeliverItemsFullyOnConsequence</c>.
///
/// The SECOND dialogue (<c>GetMerchantDialogFlow</c>, the "ambush the antagonist merchant" flow) is registered
/// from BOTH <c>QuestAcceptedConsequences()</c> AND <c>InitializeQuestOnGameLoad()</c> - the same dual-
/// registration-site shape <see cref="HeadmanNeedsToDeliverAHerdOwnershipGatePatches"/>'s doc comment already
/// documents, so a non-owner mirror peer that never ran the real accept consequence still ends up with it wired
/// up locally on its very next load/resync. Unlike the delivery flow, its top-level <c>.Condition(...)</c> IS a
/// stable named method (<c>IsSuitableToTalk</c>) - gated directly (hides the whole ambush option for a
/// non-owner, same technique <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/> uses for <c>PlayerHasTools</c>),
/// with <c>AcceptCounterOffer</c> (the actual relation/power/prosperity/quest-fail mutation) ALSO gated
/// independently as defense-in-depth, matching every other multi-layered gate in this family (e.g.
/// <c>GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches</c>).
///
/// A GENUINELY NOVEL gap independently found while auditing this (beyond anything the design critique flagged,
/// following this session's established "verify independently, don't trust the design doc" pattern): unlike
/// every other type in this family, the full-delivery dialogue branch does NOT call
/// <c>QuestBase.CompleteQuestWithSuccess()</c> from INSIDE <c>DeliverItemsFullyOnConsequence</c> itself - it
/// chains <c>base.CompleteQuestWithSuccess</c> as a SEPARATE, independent <c>.Consequence(...)</c> node later in
/// the SAME dialogue option (<c>.Consequence(DeliverItemsFullyOnConsequence).NpcLine(npcText2).Consequence(base.CompleteQuestWithSuccess)</c>).
/// Gating only <c>DeliverItemsFullyOnConsequence</c> (a Harmony prefix returning false just skips ITS OWN
/// method body) does NOT stop the dialogue's own state machine from still reaching the separately-bound
/// <c>CompleteQuestWithSuccess</c> node next - a non-owner could otherwise reach full quest completion
/// (rewards, relation, trait changes, and - critically - the generic <c>IssueFinalizedPatches</c> broadcast
/// that would tear down the REAL owner's own active quest on every peer) without ever needing to pass the
/// item-delivery gate at all. Fixed by <see cref="ArtisanOverpricedGoodsCompleteQuestGatePatch"/> below: a
/// type-scoped Harmony patch on the shared <c>QuestBase.CompleteQuestWithSuccess</c> (falls through unmodified
/// for every OTHER quest type - only this Quest type's own calls are gated).
/// </summary>
[HarmonyPatch(typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest))]
internal class ArtisanOverpricedGoodsOwnershipGatePatches
{
    [HarmonyPatch("DeliverItemsPartiallyOnConsequence")]
    [HarmonyPrefix]
    private static bool DeliverItemsPartiallyOnConsequencePrefix(
        ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("DeliverItemsFullyOnConsequence")]
    [HarmonyPrefix]
    private static bool DeliverItemsFullyOnConsequencePrefix(
        ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("IsSuitableToTalk")]
    [HarmonyPrefix]
    private static bool IsSuitableToTalkPrefix(
        ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest __instance, ref bool __result)
    {
        if (VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver)) return true;

        __result = false;
        return false;
    }

    [HarmonyPatch("AcceptCounterOffer")]
    [HarmonyPrefix]
    private static bool AcceptCounterOfferPrefix(
        ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}

/// <summary>See <see cref="ArtisanOverpricedGoodsOwnershipGatePatches"/>'s doc comment for the full derivation
/// of why this quest type specifically (unlike every sibling type in this family) needs its own gate on the
/// shared <see cref="QuestBase.CompleteQuestWithSuccess"/> rather than relying solely on gating
/// <c>DeliverItemsFullyOnConsequence</c>.</summary>
[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess))]
internal class ArtisanOverpricedGoodsCompleteQuestGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance)
    {
        if (__instance is not ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest quest) return true;

        return VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(quest.QuestGiver);
    }
}
