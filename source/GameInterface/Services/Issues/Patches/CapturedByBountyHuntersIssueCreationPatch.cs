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
/// <see cref="CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue"/> - see
/// <see cref="ICapturedByBountyHuntersIssueInterface"/>'s doc comment. Own independent postfix, same reasoning
/// as <see cref="VillageNeedsCraftingMaterialsIssueCreationPatch"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class CapturedByBountyHuntersIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue villageIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new CapturedByBountyHuntersIssueCreated(villageIssue));
    }
}

/// <summary>
/// Bug-1-shaped fix (see <see cref="ICapturedByBountyHuntersIssueInterface"/>'s doc comment for the full
/// derivation): gates the QUEST's two private map-event-driven consequence methods
/// (<c>SuccessConsequences</c>/<c>FailConsequences</c>) to the recorded owner only, so a non-owner peer whose
/// own mirrored quest object also observes a <c>MapEventEnded</c> against the same hideout (whether they
/// fought alongside the true owner, or coincidentally cleared it themselves) can never collect this issue's
/// reward/relationship/power consequences a second time.
/// </summary>
[HarmonyPatch(typeof(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest))]
internal class CapturedByBountyHuntersOwnershipGatePatches
{
    [HarmonyPatch("SuccessConsequences")]
    [HarmonyPrefix]
    private static bool SuccessConsequencesPrefix(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("FailConsequences")]
    [HarmonyPrefix]
    private static bool FailConsequencesPrefix(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
