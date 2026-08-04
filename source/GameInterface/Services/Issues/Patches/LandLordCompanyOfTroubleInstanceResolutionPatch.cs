using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Independent-check finding (design doc §3.2 flagged this as "optional" - promoted to required here since this
/// quest type's own sync mechanism, once built, makes the underlying ambiguity concrete rather than
/// theoretical): <c>company_of_trouble_menu_on_init</c> calls the private static <c>Instance</c> getter, which
/// vanilla resolves via a single behavior-wide <c>_cachedQuest</c> field + "first ongoing
/// <c>LandLordCompanyOfTroubleIssueQuest</c> found in <c>Campaign.Current.QuestManager.Quests</c>" fallback.
/// <c>QuestManager.Quests</c> contains every peer's own mirror copy of every ongoing issue of this type
/// (this type rides the fully generic accept-mirror, so a non-owner's own quest object genuinely exists and is
/// <c>IsOngoing</c> too - see <see cref="ILandLordCompanyOfTroubleIssueInterface"/>'s type doc comment) - so if
/// the SAME local player is ever the genuine owner of TWO simultaneously-accepted Company of Trouble quests (from
/// two different NPCs; nothing in vanilla or this allowlist prevents that), <c>Instance</c> could resolve to the
/// WRONG one, since "first found" has no relationship to "the one whose own HourlyTick just opened this menu".
///
/// Unlike <c>MerchantArmyOfPoachersInstanceResolutionPatch</c> (no location field to key off) or the design
/// doc's own more speculative "prefer whichever candidate has a transient flag set" proposal, this quest type
/// already has a strictly better, already-populated, already-reliable signal available by the time this getter
/// is ever called: <see cref="VillageNeedsToolsIssueOwnership.IsLocalPeerOwner"/> - populated at accept time (long
/// before the player could ever reach <c>company_of_trouble_menu</c>) by the same generic accept-mirror broadcast
/// every other type in this family already relies on. A mirrored copy of some OTHER player's concurrently-active
/// Company of Trouble quest (same type, different lord) is never owned by this local peer, so it can never be
/// the wrong resolution target - same reasoning/precedent as <c>BettingFraudInstanceResolutionPatch</c>.
///
/// Left alone (runs the untouched vanilla getter) whenever <see cref="CallOriginalPolicy.IsOriginalAllowed"/> is
/// true, matching every other patch in this issue family.
/// </summary>
[HarmonyPatch(typeof(LandLordCompanyOfTroubleIssueBehavior), "Instance", MethodType.Getter)]
internal class LandLordCompanyOfTroubleInstanceResolutionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        __result = null;

        foreach (QuestBase quest in Campaign.Current.QuestManager.Quests)
        {
            if (quest is LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest candidate
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
