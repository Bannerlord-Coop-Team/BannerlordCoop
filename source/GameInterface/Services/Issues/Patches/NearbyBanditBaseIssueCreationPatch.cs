using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts a genuine server-side creation of a
/// <see cref="NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue"/> - see
/// <see cref="INearbyBanditBaseIssueInterface"/>'s doc comment. Own independent postfix, same reasoning as
/// <see cref="VillageNeedsCraftingMaterialsIssueCreationPatch"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class NearbyBanditBaseIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue banditIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new NearbyBanditBaseIssueCreated(banditIssue));
    }
}

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation, and <see cref="CapturedByBountyHuntersOwnershipGatePatches"/> for the closest-shaped precedent):
/// <c>NearbyBanditBaseIssueQuest</c>'s resolution is not dialogue-driven at all - it resolves via a
/// <c>MapEventEnded</c>/<c>WarDeclared</c> listener reacting to whichever peer's own local combat/diplomacy
/// happens to satisfy the condition. Since every peer's own mirrored quest object independently listens for
/// these (symmetric, campaign-state-driven events that fire identically everywhere), the three REWARD-bearing
/// consequence methods (<c>OnQuestSucceeded</c>/<c>OnQuestFailed</c>/<c>WarCausedByPlayerFail</c> - relation,
/// power, prosperity/security changes) are gated to the recorded owner only, so a non-owner peer whose own
/// mirrored quest object also observes the same map event/war declaration can never collect these
/// consequences a second time. <c>OnQuestCanceled</c>/<c>OnHideoutCleared</c> are deliberately NOT gated here -
/// neither applies any reward/penalty beyond the (already idempotent-safe, already-generically-mirrored) cancel
/// itself, matching the same "only gate reward-applying consequences" precedent already established for every
/// other type in this project.
/// </summary>
[HarmonyPatch(typeof(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest))]
internal class NearbyBanditBaseOwnershipGatePatches
{
    [HarmonyPatch("OnQuestSucceeded")]
    [HarmonyPrefix]
    private static bool OnQuestSucceededPrefix(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnQuestFailed")]
    [HarmonyPrefix]
    private static bool OnQuestFailedPrefix(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("WarCausedByPlayerFail")]
    [HarmonyPrefix]
    private static bool WarCausedByPlayerFailPrefix(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
