using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts a genuine server-side <c>IssueManager.CreateNewIssue</c> creating a
/// <see cref="LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue"/> - see
/// <see cref="ILordNeedsHorsesIssueInterface"/>'s doc comment. Deliberately its own independent postfix (same
/// reasoning as <see cref="VillageNeedsCraftingMaterialsIssueCreationPatch"/>): the client-creation-blocking
/// Prefix on <see cref="IssueManagerCreateNewIssuePatches"/> is already fully generic.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class LordNeedsHorsesIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue villageIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new LordNeedsHorsesIssueCreated(villageIssue));
    }
}

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation): <c>LordNeedsHorsesIssueQuest</c> has not one but TWO separate turn-in dialogue locations
/// (the main quest-giver's own <c>DiscussDialogFlow</c>, AND a garrison-commander NPC conversation opened by
/// <c>OnSettlementEntered</c> for any settlement owned by the quest-giver's clan - a wrinkle the survey
/// missed) - but both inline their satisfied-condition as the exact same expression,
/// <c>GetNumQuestMountsInInventory() &gt;= _numMountsToBeDelivered</c>, with no separately-named boolean method
/// to gate the way <c>PlayerHasTools()</c>/<c>CompleteQuestClickableConditions()</c> are for the other two
/// types. Patching the shared underlying <c>GetNumQuestMountsInInventory()</c> accessor instead - forcing it
/// to report 0 (always &lt; <c>_numMountsToBeDelivered</c>, which is always &gt;= 1) for a non-owner - closes
/// BOTH turn-in locations with one gate. Harmless for the owner's own <c>OnCompleteWithSuccess()</c>/
/// <c>HourlyTick()</c>/<c>CheckAndHandleQuestSuccessConditions()</c> calls to this same method: those only
/// ever run once the real, non-gated (owner-only) path has already let a turn-in through.
/// </summary>
[HarmonyPatch(typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest), "GetNumQuestMountsInInventory")]
internal class LordNeedsHorsesQuestOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest __instance, ref int __result)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = 0;
            return false; // skip the original - non-owners can never appear to have enough mounts
        }

        return true; // real owner: run the real check unmodified
    }
}
