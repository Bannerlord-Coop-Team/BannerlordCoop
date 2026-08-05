using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug 1 fix: nothing previously recorded which specific connected player's client genuinely accepted a
/// <c>VillageNeedsToolsIssue</c>'s quest solution, so ANY peer whose own roster happened to satisfy
/// <c>PlayerHasTools</c> could turn the quest in and collect the reward - even though every peer, after
/// Finding 1's accept-sync fix, now has its own real, locally-registered <c>DiscussDialogFlow</c> for the
/// same logical issue (see <see cref="Interfaces.VillageNeedsToolsIssueInterface"/>'s doc comment).
///
/// Gates the dialogue CONDITION itself rather than adding a round-trip: it's a pure, cheap, local boolean
/// check against already-synced state (<see cref="VillageNeedsToolsIssueOwnership"/>, populated by the
/// existing accept broadcast - see <see cref="Handlers.VillageNeedsToolsIssueHandler"/>), so it needs no new
/// network round-trip, no "waiting" UI state, and cannot itself desync anything since it only ever *hides*
/// an option, never changes how the conversation runs. A non-owner's conversation still opens (harmless -
/// the greeting line and "I'm still looking for your goods." option have no gating condition and no
/// consequence) - only "Yes, I brought your tools." becomes permanently unsatisfiable for them, so
/// <c>FinishQuestSuccess1()</c> (which deducts items, pays gold/exchange goods, and completes the quest)
/// becomes unreachable for anyone except the recorded owner.
/// </summary>
[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest), "PlayerHasTools")]
internal class VillageNeedsToolsQuestOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest __instance, ref bool __result)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = false;
            return false; // skip the original - non-owners never see/can-select the option
        }

        return true; // real owner: run the real check unmodified
    }
}
