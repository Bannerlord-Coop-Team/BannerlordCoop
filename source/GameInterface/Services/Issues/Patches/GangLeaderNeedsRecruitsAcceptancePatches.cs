using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures this accept's authoritative <c>_requestedRecruitCount</c> - see
/// <see cref="IGangLeaderNeedsRecruitsIssueInterface"/>'s doc comment. Own independent postfix, same shape as
/// <see cref="VillageNeedsCraftingMaterialsQuestAcceptancePatch"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class GangLeaderNeedsRecruitsQuestAcceptancePatch
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue) return;

        if (!ContainerProvider.TryResolve<IGangLeaderNeedsRecruitsIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var requestedRecruitCount)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new GangRecruitsIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId, requestedRecruitCount));
    }
}

/// <summary>
/// Bug-1-shaped fix: nothing gates WHO is allowed to open the recruit-delivery party screen
/// (<c>OpenRecruitDeliveryScreen</c>) for a given hero's Gang Leader Needs Recruits issue - any peer's own
/// mirrored quest object could independently transfer THEIR OWN bandit recruits and advance
/// <c>_deliveredRecruitCount</c>/<c>_rewardGold</c> on their own copy, eventually satisfying
/// <c>_playerReachedRequestedAmount</c> and collecting the reward that should go to the recorded owner. Gates
/// the one entry point that starts this local progress.
/// </summary>
[HarmonyPatch(typeof(GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssueQuest), "OpenRecruitDeliveryScreen")]
internal class GangLeaderNeedsRecruitsOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
