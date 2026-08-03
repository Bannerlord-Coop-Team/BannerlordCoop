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
/// Ordinary coop play can have TWO different players' LordNeedsGarrisonTroopsIssueQuest instances active at
/// once (accepted from two different Lords, each with their own <c>_settlement</c>) - both mirrored onto every
/// peer's shared <c>QuestManager.Quests</c> list. Vanilla's <c>Instance</c> deterministically resolves to
/// whichever was created first, globally, for the rest of the session (the cache never re-evaluates once set
/// to an <c>IsOngoing</c> quest). For the SECOND player this is a full soft-lock: at their own settlement,
/// <c>Settlement.CurrentSettlement == Instance._settlement</c> would compare their settlement against the
/// FIRST player's <c>_settlement</c>, deterministically fail, and "Talk to the garrison commander" would never
/// even appear for them - the sole turn-in path for this issue type - permanently, regardless of the separate
/// ownership gate already on <see cref="LordNeedsGarrisonTroopsQuestOwnershipGatePatch"/> (which never gets a
/// chance to run).
///
/// Fixed by resolving <c>Instance</c> to whichever ongoing quest's own <c>_settlement</c> actually matches
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
