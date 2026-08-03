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
/// <see cref="HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue"/> - see
/// <see cref="IHeadmanNeedsToDeliverAHerdIssueInterface"/>'s doc comment. Deliberately its own independent
/// postfix (same reasoning as <see cref="HeadmanVillageNeedsDraughtAnimalsIssueCreationPatch"/>): the
/// client-creation-blocking Prefix on <see cref="IssueManagerCreateNewIssuePatches"/> is already fully generic.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class HeadmanNeedsToDeliverAHerdIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue herdIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new HeadmanNeedsToDeliverAHerdIssueCreated(herdIssue));
    }
}
