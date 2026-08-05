using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Real, independently-verified bug (the exact shape flagged for verification going into this task, matching
/// <c>GangLeaderNeedsWeaponsQuestOwnershipGatePatch</c>/<c>LordWantsRivalCapturedOwnershipGatePatches</c>):
/// <c>SmugglersIssueQuest.GetSmugglerPartyDialog()</c>'s "bribe"/persuasion-success options (and the shared
/// fight-win path) all funnel into the private <c>SucceedQuest(TextObject log)</c>, with no ownership check
/// anywhere in the chain.
///
/// This dialogue is registered on EVERY peer, not just the genuine accepter: <c>SetDialogs()</c> - which calls
/// <c>Campaign.Current.ConversationManager.AddDialogFlow(GetSmugglerPartyDialog(), this)</c> - runs
/// unconditionally from the <c>SmugglersIssueQuest</c> constructor (both the genuine accepter's real
/// construction AND every OTHER peer's own mirror, built via <c>GenericAcceptMirrorIssueTypes</c>'s bare
/// <c>IssueManager.StartIssueQuest</c> replay - see <see cref="Interfaces.ISmugglersIssueInterface"/>'s type
/// doc comment). Its own <c>Condition</c> only checks <c>_smugglerParty != null &amp;&amp;
/// CharacterObject.OneToOneConversationCharacter == ConversationHelper.GetConversationCharacterPartyLeader(_smugglerParty.Party)</c>
/// - and <c>_smugglerParty</c> is now (correctly) force-mirrored onto EVERY peer once
/// <see cref="SmugglersPartySpawnGatePatch"/>'s broadcast arrives (it's a real, globally-visible hostile
/// party any connected player can physically walk up to and talk to, independent of who owns the quest). So
/// ANY connected player who talks to the shared smuggler party leader and either bribes them
/// (<c>SucceedQuestWithBribe</c>) or completes the persuasion minigame
/// (<c>persuasion_complete_with_smugglers_on_consequence</c>) can complete and collect someone ELSE'S
/// already-accepted quest on their own machine - <c>GiveGoldAction</c>/<c>CompleteQuestWithSuccess()</c>/
/// <c>DestroyPartyAction.Apply</c> all run there, a genuine cross-peer exploit and desync source.
///
/// Fix: gate the shared completion method itself (the "gate the actual mutation, not each
/// registration/condition site" principle this codebase already established for
/// <c>GangLeaderNeedsToOffloadStolenGoods</c>/<c>LordWantsRivalCaptured</c>) rather than each of the two
/// dialogue entry points individually - this also covers the fight-win path
/// (<c>OnMapEventEnded</c> -&gt; <c>SucceedQuest</c>) for free, though that path was already safe on its own
/// (only ever wired via <c>RegisterEvents</c>, itself only ever called by <c>StartQuest()</c> inside
/// <c>QuestAcceptedConsequences</c> - Category A/B, genuine-accepter-only). <c>FailQuest</c> is deliberately
/// NOT gated - its only caller, <c>OnTimedOut()</c>, is already RegisterEvents-gated to the genuine owner, the
/// same reasoning <c>LordWantsRivalCapturedOwnershipGatePatches</c> already documents for its own
/// non-gated <c>FirstCounterOfferFinished()</c>.
/// </summary>
[HarmonyPatch(typeof(SmugglersIssueBehavior.SmugglersIssueQuest))]
internal class SmugglersQuestOwnershipGatePatch
{
    [HarmonyPatch("SucceedQuest")]
    [HarmonyPrefix]
    private static bool SucceedQuestPrefix(SmugglersIssueBehavior.SmugglersIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
