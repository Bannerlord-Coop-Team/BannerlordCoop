using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using SandBox.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Fixes vanilla's campaign-wide single-slot cache/first-found <c>Instance</c> resolution
/// (<c>RivalGangMovingInIssueBehavior.Instance</c>'s own private static getter body: <c>_cachedQuest</c>/"first
/// <c>RivalGangMovingInIssueQuest</c> found in <c>QuestManager.Quests</c>") - exact same precedent/shape as
/// <see cref="BettingFraudInstanceResolutionPatch"/>, see that type's doc comment for the general derivation.
///
/// For this quest specifically: ordinary concurrent play can't cause two owners' quests to collide (a
/// non-owner's mirror <c>RivalGangMovingInIssueQuest</c> is inert by construction - <c>IssueBase.StartIssueWithQuest</c>
/// never calls <c>QuestBase.StartQuest()</c>, so it's never <c>IsOngoing</c>/never in <c>Quests</c>). The real,
/// narrower risk: <c>QuestManager._quests</c> is a raw <c>[SaveableField]</c>, restored directly by the
/// serializer on load, bypassing <c>StartQuest()</c>/<c>OnQuestStarted()</c> - then
/// <c>QuestManager.OnGameLoaded()</c> DOES call <c>InitializeQuestOnLoadWithQuestManager()</c> ->
/// <c>RegisterEvents()</c> unconditionally for every non-finalized quest whose matching <c>IssueBase</c> is found
/// in <c>IssueManager.Issues</c> - which, given this mod's per-peer-replicated-issue architecture, generally IS
/// present for every owner's issue on every peer. So a mid-session save transfer (or a save/reload while two
/// different owners' quests are concurrently active) can genuinely leave a peer's own <c>_quests</c> holding MORE
/// THAN ONE owner's <c>RivalGangMovingInIssueQuest</c>, each genuinely <c>IsOngoing</c> - at which point vanilla's
/// single-slot "first found" <c>Instance</c> getter has no principled way to pick the right one for whichever
/// menu/dialogue context is live on this peer. See doc/RivalGangMovingIn_Design_v2.md §3 for the full derivation.
///
/// All 4 of vanilla's <c>Instance</c>-consuming call sites are covered automatically since they all read the
/// same static getter: <c>rival_gang_wait_duration_is_over_menu_on_init</c>,
/// <c>rival_gang_quest_wait_duration_is_over_yes_consequence</c>, <c>rival_gang_quest_before_fight_init</c>,
/// <c>rival_gang_quest_after_fight_init</c>, plus the Quest's own <c>HourlyTick</c>.
///
/// Residual, out-of-scope risk (matching this codebase's own established practice for this exact class of gap -
/// see <c>IssueManagerTickPatches</c>'s and Lord Wants Rival Captured's own documented, unsolved save/reload-
/// <c>RegisterEvents()</c>-reentry limitations): the same save/reload leak also re-registers this quest's 2
/// dynamically-added dialogue flows (<c>GetRivalGangLeaderDialogFlow</c>/<c>GetQuestGiverPreparationCompletedDialogFlow</c>,
/// added inside <c>InitializeQuestOnGameLoad()</c>) on a peer that may not be the true owner - and, independently
/// found beyond the design doc's own note, the Quest's own <c>HourlyTick</c> checks conditions against whatever
/// <c>Instance</c> now resolves to (the LOCAL PEER'S genuinely-owned quest, thanks to this fix) but then calls
/// <c>this.OnGuestGiverPreparationsCompleted()</c> - i.e. if a single peer's own <c>_quests</c> leaked TWO
/// <c>IsOngoing</c> Rival Gang quests (one genuinely theirs, one a leaked mirror of someone else's), the leaked
/// mirror's own <c>HourlyTick</c> could still incorrectly self-trigger using the genuinely-owned quest's timing.
/// A full fix needs the same bigger, cross-cutting "who is allowed to drive a foreign quest object that leaked
/// into my own <c>_quests</c>" architectural question already flagged unresolved elsewhere in this project - out
/// of scope for this pass. §7's ownership gates (<see cref="RivalGangMovingInOwnershipGatePatches"/>) close the
/// 2 REWARD-bearing paths this leak could reach, as defense-in-depth, not a full solution to the underlying leak.
/// </summary>
[HarmonyPatch(typeof(RivalGangMovingInIssueBehavior), "Instance", MethodType.Getter)]
internal class RivalGangMovingInInstanceResolutionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        __result = null;

        foreach (QuestBase quest in Campaign.Current.QuestManager.Quests)
        {
            if (quest is RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest candidate
                && candidate.IsOngoing
                && VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(candidate.QuestGiver))
            {
                __result = candidate;
                break;
            }
        }

        return false; // skip the original cache/first-found lookup entirely
    }
}
