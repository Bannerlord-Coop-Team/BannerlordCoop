using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation), for the genuinely novel mechanism this quest type introduces: a THIRD <c>DialogFlow</c>
/// (<c>GetDeliveryDialogFlow</c>), registered OUTSIDE the normal <c>SetDialogs</c>/<c>AddDialogs</c> hook
/// entirely via a direct, imperative <c>Campaign.Current.ConversationManager.AddDialogFlow(...)</c> call -
/// from TWO separate places: <c>QuestAcceptedConsequences()</c> (the real accepter's own live conversation)
/// AND <c>InitializeQuestOnGameLoad()</c> (called via <c>QuestBase.InitializeQuestOnLoadWithQuestManager</c>
/// on EVERY peer, owner and non-owners alike, on every campaign load/resync - this is the load-bearing gap: a
/// non-owner peer's own mirrored quest object never sees <c>QuestAcceptedConsequences()</c> run at accept
/// time (see <see cref="IHeadmanNeedsToDeliverAHerdIssueInterface"/>'s doc comment), but it DOES get this same
/// delivery flow re-registered locally the very next load, with no ownership concept at all).
///
/// This second dialogue is anchored at the SECOND settlement/NPC (<c>_targetSettlement</c>/<c>_targetHero</c>),
/// entirely separate from the issue giver's own settlement - so any peer whose player character happens to
/// physically walk up to that NPC in THEIR OWN local simulation can open it, regardless of who actually
/// accepted the quest. Neither of its two "have you brought the herd" player-option branches has a stable
/// named condition method to gate the way e.g. <c>VillageNeedsToolsIssueQuest.PlayerHasTools</c> is gated
/// (the item-count check is an anonymous inline <c>.Condition(delegate {...})</c>, and the reject branch -
/// "I'm going to keep that herd for myself" - has NO condition at all, always selectable) - so, same shape as
/// <see cref="HeadmanNeedsGrainOwnershipGatePatches"/>'s <c>Success()</c>/<c>TimeoutFail()</c> gates, this
/// patches the two actual state-mutating <c>Consequence</c> methods themselves directly:
///
/// - <c>DeliverHerdOnConsequence</c> -&gt; <c>CompleteQuestWithSuccess()</c> (deducts the herd from the LOCAL
///   <c>PartyBase.MainParty.ItemRoster</c>, moves it into <c>_targetSettlement</c>, pays the real gold reward).
/// - <c>DeliverHerdRejectOnConsequence</c> -&gt; <c>CompleteQuestWithFail()</c> + a crime-rating hit. This one
///   is explicitly called out by the task: it requires NO items at all ("I'm going to keep that herd for
///   myself"), so without this gate ANY non-owner peer who merely wanders up to the target NPC could
///   force-fail someone else's already-accepted quest for free, with no ownership check to stop them - the
///   exact same shape of bug as the missing turn-in gate on the success branch, just reachable without
///   needing any items in hand at all.
///
/// Gating the Consequence methods directly (rather than only the dialogue Condition) is also what makes this
/// exhaustive regardless of WHICH of the two registration call sites put the flow there - the fix does not
/// need to know or care whether this peer's copy of <c>GetDeliveryDialogFlow()</c> came from a genuine local
/// accept or a post-load re-registration; either way, the actual reward/fail mutation is unreachable for
/// anyone but the recorded owner.
/// </summary>
[HarmonyPatch(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest))]
internal class HeadmanNeedsToDeliverAHerdOwnershipGatePatches
{
    [HarmonyPatch("DeliverHerdOnConsequence")]
    [HarmonyPrefix]
    private static bool DeliverHerdOnConsequencePrefix(
        HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("DeliverHerdRejectOnConsequence")]
    [HarmonyPrefix]
    private static bool DeliverHerdRejectOnConsequencePrefix(
        HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
