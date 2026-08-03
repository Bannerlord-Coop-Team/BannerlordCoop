using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Fixes vanilla's campaign-wide single-slot cache/first-found <c>Instance</c> resolution (see that private
/// static getter's own body: <c>_cachedQuest</c>/"first LordNeedsGarrisonTroopsIssueQuest found in
/// QuestManager.Quests") that BOTH <c>talk_to_garrison_commander_on_condition</c> AND
/// <c>talk_to_garrison_commander_on_consequence</c> rely on - the only two call sites of <c>Instance</c> in the
/// whole behavior class. Both menu callbacks are globally registered once (<c>OnSessionLaunched</c>), not per
/// quest instance, and the option itself appears unconditionally on every peer's "town"/"town_guard"/
/// "castle_guard" menu regardless of which quest (if any) is locally relevant.
///
/// Correction (traced end-to-end against the real decompiled vanilla source and this mod's own mirror path,
/// see project notes): a mirrored copy of another player's LordNeedsGarrisonTroopsIssueQuest is NOT added to
/// this peer's own <c>QuestManager.Quests</c> - that list is only ever populated by <c>QuestBase.StartQuest()</c>
/// (via <c>QuestManager.OnQuestStarted</c>), and <c>StartQuest()</c> only ever runs on the machine whose own
/// live dialogue genuinely accepted the quest. This mod's mirror/replay path (and the full-save transfer a
/// joining client receives) only ever reaches <c>IssueManager.StartIssueQuest</c>/<c>IssueBase.StartIssueWithQuest</c>,
/// which construct the Quest object but never call <c>StartQuest()</c>. So the originally-suspected "two
/// players' quests colliding in one shared list" scenario is not actually reachable today - each peer's own
/// <c>QuestManager.Quests</c>, filtered to this type, only ever contains that peer's own genuinely-accepted
/// quest (if any). This patch is kept anyway since resolving by settlement is more correct in principle than
/// vanilla's first-found/cached behavior and costs nothing, but it is not fixing a bug that was actually
/// reachable via that mechanism - treat it as a robustness improvement, not a confirmed-exploitable-bug fix.
///
/// Resolves <c>Instance</c> to whichever ongoing quest's own <c>_settlement</c> actually matches
/// <see cref="Settlement.CurrentSettlement"/> - the settlement the local player is physically standing in when
/// this menu is evaluated - rather than "first found"/cached. This is purely local (no network round-trip):
/// each peer's own client just needs to find whichever campaign-wide quest is contextually relevant to where
/// IT currently stands, and <c>_settlement</c> is plain synced world state, not per-peer ownership info. The
/// deeper "hand over the troops" click stays correctly gated by the pre-existing
/// <see cref="LordNeedsGarrisonTroopsQuestOwnershipGatePatch"/>, so a non-owning peer who is merely standing in
/// the right settlement still cannot complete someone else's quest.
///
/// Left alone (runs the untouched vanilla getter) whenever <see cref="CallOriginalPolicy.IsOriginalAllowed"/>
/// is true, matching every other patch in this issue family.
/// </summary>
[HarmonyPatch(typeof(LordNeedsGarrisonTroopsIssueQuestBehavior), "Instance", MethodType.Getter)]
internal class LordNeedsGarrisonTroopsInstanceResolutionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        __result = null;

        var currentSettlement = Settlement.CurrentSettlement;
        if (currentSettlement == null) return false;

        foreach (QuestBase quest in Campaign.Current.QuestManager.Quests)
        {
            if (quest is LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest candidate
                && candidate.IsOngoing
                && candidate._settlement == currentSettlement)
            {
                __result = candidate;
                break;
            }
        }

        return false; // skip the original cache/first-found lookup entirely
    }
}
