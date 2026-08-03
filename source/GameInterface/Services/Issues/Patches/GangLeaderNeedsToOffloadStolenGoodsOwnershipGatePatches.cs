using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation) for the SECOND-LOCATION (the hideout, <c>_questHideout</c>) mechanism this quest type introduces.
///
/// Unlike Deliver the Herd's second dialogue, the hideout-leader dialogue's own pay/attack options
/// (<c>GetHideoutLeaderDialogFlow</c>) use ANONYMOUS <c>.Consequence(delegate {...})</c> lambdas with no stable
/// named method to Harmony-patch directly - but setting <c>_isPayingForGoods</c>/<c>_isFightingForGoods</c>/
/// <c>_playerHasTheGoods</c> on a non-owner's own LOCAL mirror copy of the quest is harmless in isolation (no
/// network effect, nothing granted). The REAL exploit surface is the five NAMED private methods that actually
/// cascade into a reward/relation/trait mutation AND (via <c>CompleteQuestWithSuccess</c>/<c>WithBetrayal</c>/
/// <c>WithFail</c>) the network-visible finalize broadcast every allowlisted issue type now gets generically -
/// gating these five closes the exploit regardless of whichever anonymous lambda or event
/// (<c>OnHideoutBattleCompleted</c>) reached them, exactly like
/// <see cref="HeadmanNeedsToDeliverAHerdOwnershipGatePatches"/>'s doc comment argues for gating the actual
/// Consequence over the registration site:
///
/// - <c>SucceedQuestByPayingAndKeepingTheGoods</c> / <c>SucceedQuestByPayingAndGivingTheGoodsBack</c> - the
///   after-hideout merchant dialogue's success branches (free stolen goods + gold + trait/relation gains for
///   whoever reaches them).
/// - <c>FailQuestByKeepingTheGoods</c> / <c>FailQuestByGivingBackTheGoods</c> - the same dialogue's betrayal
///   branches (still grants the stolen goods to whoever's local party is holding them).
/// - <c>FailQuestByLosingHideoutBattle</c> - reached from the <c>OnHideoutBattleCompletedEvent</c> listener,
///   which (like every quest's own <c>RegisterEvents</c>) fires on EVERY peer holding a mirrored copy of this
///   quest, not just the owner - a non-owner peer whose own local combat happens to resolve at the shared
///   <c>_questHideout</c> settlement could otherwise fail someone else's already-accepted quest for free.
///
/// ALSO gates <c>OnSettlementLeft</c> itself (defense-in-depth + UX correctness, same reasoning as
/// <see cref="ArtisanCantSellProductsAtAFairPriceOwnershipGatePatches"/>'s <c>BeforeGameMenuOpened</c> gate):
/// this method PROACTIVELY force-opens a conversation with <c>_counterOfferHero</c> (or the hideout's bandit
/// leader flow) purely from leaving either settlement, with no ownership concept at all - without this, a
/// non-owner mirror peer's own local player leaving the shared hideout or the counter-offer hero's settlement
/// would be ambushed into a conversation meant only for the recorded owner, purely from holding a mirrored
/// quest object.
///
/// <c>IsSettlementBusy</c> (both the Issue-level and Quest-level overrides) is deliberately NOT gated - it is a
/// read-only, non-mutating query (just reports a priority to whichever caller asks "is this settlement busy"),
/// answers consistently for every peer off the same already-synced <c>_issueHideout</c>/<c>_questHideout</c>
/// reference, and gating it would only risk breaking the vanilla "don't let two quests compete for the same
/// hideout" protection it exists to provide.
/// </summary>
[HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest))]
internal class GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches
{
    [HarmonyPatch("SucceedQuestByPayingAndKeepingTheGoods")]
    [HarmonyPrefix]
    private static bool SucceedQuestByPayingAndKeepingTheGoodsPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("SucceedQuestByPayingAndGivingTheGoodsBack")]
    [HarmonyPrefix]
    private static bool SucceedQuestByPayingAndGivingTheGoodsBackPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("FailQuestByKeepingTheGoods")]
    [HarmonyPrefix]
    private static bool FailQuestByKeepingTheGoodsPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("FailQuestByGivingBackTheGoods")]
    [HarmonyPrefix]
    private static bool FailQuestByGivingBackTheGoodsPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("FailQuestByLosingHideoutBattle")]
    [HarmonyPrefix]
    private static bool FailQuestByLosingHideoutBattlePrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnSettlementLeft")]
    [HarmonyPrefix]
    private static bool OnSettlementLeftPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}

/// <summary>
/// The task's second flagged price-sync gap, fixed properly (see
/// <see cref="IGangLeaderNeedsToOffloadStolenGoodsIssueInterface"/>'s type doc comment for the full derivation):
/// <c>StolenTradeGoodAmount</c>/<c>RewardGold</c> (Issue-level, no backing field to force) are patched to return
/// the frozen value captured at genuine alternative-solution-accept time
/// (<see cref="GangLeaderStolenGoodsAlternativeSolutionFreeze"/>) instead of re-deriving live from
/// <c>Town.GetItemPrice</c> every time they're evaluated - closing the same "authoritative value must be
/// captured once, not re-derived later" gap the quest-solution path's <see cref="Interfaces.GangLeaderNeedsToOffloadStolenGoodsIssueInterface.MirrorQuestAccepted"/>
/// already closes for that path. Falls back to the real computation whenever no freeze is recorded yet (before
/// any alternative-solution accept, and for the QUEST-SOLUTION path, which never populates this freeze) - so
/// this is a pure no-op until an alternative solution has genuinely been accepted.
/// </summary>
[HarmonyPatch]
internal class GangLeaderNeedsToOffloadStolenGoodsPriceFreezePatches
{
    [HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), "StolenTradeGoodAmount", MethodType.Getter)]
    [HarmonyPrefix]
    private static bool StolenTradeGoodAmountPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue __instance, ref int __result)
    {
        if (!GangLeaderStolenGoodsAlternativeSolutionFreeze.TryGetFrozenAmount(__instance.IssueOwner, out var frozen)) return true;

        __result = frozen;
        return false;
    }

    [HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), "RewardGold", MethodType.Getter)]
    [HarmonyPrefix]
    private static bool RewardGoldPrefix(
        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue __instance, ref int __result)
    {
        if (!GangLeaderStolenGoodsAlternativeSolutionFreeze.TryGetFrozenReward(__instance.IssueOwner, out var frozen)) return true;

        __result = frozen;
        return false;
    }
}

/// <summary>
/// Drains <see cref="GangLeaderStolenGoodsAlternativeSolutionFreeze"/> on every genuine or mirrored
/// <c>IssueFinalized</c> for this issue type, so a stale frozen amount never leaks into this hero's NEXT,
/// unrelated issue - the same leak concern <see cref="IssueFinalizedPatches"/>'s doc comment already documents
/// for <see cref="Patches.IssueManagerQuestCompletedReasonCapture.PendingReasons"/>/<see cref="VillageNeedsToolsIssueOwnership"/>.
/// Deliberately its own independent postfix (Harmony ANDs multiple independent postfixes on the same method
/// without conflict) rather than a change to the shared <see cref="IssueFinalizedPatches"/> file, keeping this
/// feature self-contained the same way <see cref="Interfaces.VillageNeedsCraftingMaterialsIssueInterface.RejectAcceptance"/>'s
/// doc comment argues for.
/// </summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class GangLeaderNeedsToOffloadStolenGoodsFinalizeCleanupPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (__instance is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue) return;

        GangLeaderStolenGoodsAlternativeSolutionFreeze.Clear(__instance.IssueOwner);
    }
}
