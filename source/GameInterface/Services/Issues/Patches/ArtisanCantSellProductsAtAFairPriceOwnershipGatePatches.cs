using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation) for this quest type's TWO genuinely novel mechanisms, both registered OUTSIDE the normal
/// <c>SetDialogs</c>/<c>AddDialogs</c> hook, from the SAME two call sites (<c>QuestAcceptedConsequences()</c>
/// AND <c>InitializeQuestOnGameLoad()</c> - the identical dual-registration-site shape
/// <see cref="HeadmanNeedsToDeliverAHerdOwnershipGatePatches"/>'s doc comment already documents, so a non-owner
/// mirror peer that never ran the real accept consequence still ends up with both flows wired up locally on
/// its very next load/resync):
///
/// 1. <c>GetDeliveryDialogFlow</c> - the SECOND-LOCATION dialogue, anchored at <c>_targetSettlement</c>/
///    <c>_targetHero</c>, gated the same way Deliver the Herd's own delivery flow is: patch the actual
///    state-mutating <c>Consequence</c> methods directly (<c>DeliverItemsPartiallyOnConsequence</c>,
///    <c>DeliverItemsFullyOnConsequence</c>, <c>DeliverItemsRejectOnConsequence</c> - THREE branches here,
///    since unlike Deliver the Herd this dialogue also has a partial-delivery path), rather than the dialogue's
///    own inline, unnamed <c>.Condition(delegate {...})</c> checks (no stable named condition method exists to
///    gate the way e.g. <c>VillageNeedsToolsIssueQuest.PlayerHasTools</c> is).
///
/// 2. <c>GetCounterOfferDialogFlow</c> - the task's "ambush counter-offer": unlike the delivery flow, its OWN
///    <c>.Condition(...)</c> checks ONLY <c>_counterOfferHero == Hero.OneToOneConversationHero &amp;&amp;
///    !_counterOfferRefused</c> - no settlement gate at all, so it can fire wherever the player's own local
///    conversation happens to reach <c>_counterOfferHero</c>. Worse: <c>BeforeGameMenuOpened</c> (wired via
///    <c>RegisterEvents</c> -&gt; <c>CampaignEvents.BeforeGameMenuOpenedEvent</c>, which - like every quest's
///    <c>RegisterEvents</c> - fires on EVERY peer holding a mirrored copy of this quest, owner and non-owner
///    alike) PROACTIVELY force-opens a conversation with <c>_counterOfferHero</c> the next time ANY menu opens
///    anywhere, with no ownership concept at all - so without this gate, a non-owner mirror peer would
///    genuinely get ambushed into this conversation on their own machine purely from holding a mirrored quest
///    object, never having walked up to anyone. Gating <c>BeforeGameMenuOpened</c> itself (in addition to the
///    two real state-mutating consequences the resulting dialogue can reach,
///    <c>QuestFailedWithRefusal</c>/<c>RefuseCounterOfferConsequences</c>) closes both the UX hijack AND the
///    underlying exploit (a non-owner could otherwise hand over/refuse someone else's goods, mutating
///    relation/crime-rating state and completing or failing someone else's already-accepted quest for free).
/// </summary>
[HarmonyPatch(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest))]
internal class ArtisanCantSellProductsAtAFairPriceOwnershipGatePatches
{
    [HarmonyPatch("BeforeGameMenuOpened")]
    [HarmonyPrefix]
    private static bool BeforeGameMenuOpenedPrefix(
        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("DeliverItemsPartiallyOnConsequence")]
    [HarmonyPrefix]
    private static bool DeliverItemsPartiallyOnConsequencePrefix(
        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("DeliverItemsFullyOnConsequence")]
    [HarmonyPrefix]
    private static bool DeliverItemsFullyOnConsequencePrefix(
        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("DeliverItemsRejectOnConsequence")]
    [HarmonyPrefix]
    private static bool DeliverItemsRejectOnConsequencePrefix(
        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("QuestFailedWithRefusal")]
    [HarmonyPrefix]
    private static bool QuestFailedWithRefusalPrefix(
        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("RefuseCounterOfferConsequences")]
    [HarmonyPrefix]
    private static bool RefuseCounterOfferConsequencesPrefix(
        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}

/// <summary>
/// Disables the (entirely unsynced - no <c>IssueBase.StartIssueWithLordSolution</c> mechanism exists anywhere
/// in this project, same gap <see cref="LadysKnightOutIssueInterface"/>'s doc comment documents) Lord Solution
/// path for this issue type by forcing its <c>IsThereLordSolution</c> getter to always report false. Vanilla's
/// own dialogue system already gates the whole "use your influence instead" option on this property, so this
/// alone is enough to hide it - no other change needed. Deliberately independent of the Lord Solution's own
/// built-in counter-offer flow (handled generically by vanilla's <c>LordConversationsCampaignBehavior</c> when
/// <c>IsThereLordSolution</c> is true) - the task's "ambush counter-offer" is the UNRELATED Quest-level
/// <c>GetCounterOfferDialogFlow</c> mechanic gated above, which fires on the QUEST-SOLUTION path and remains
/// fully active regardless of this disable.
/// </summary>
[HarmonyPatch(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue), "IsThereLordSolution", MethodType.Getter)]
internal class ArtisanCantSellProductsAtAFairPriceLordSolutionDisablePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}
