using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts the result of a genuine server-side <c>IssueManager.CreateNewIssue</c> creating a
/// <see cref="MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue"/>, so every client replicates
/// the exact same picked quest village instead of independently re-rolling
/// <c>Village.GetRandomElementWithPredicate</c> locally - see
/// <see cref="Interfaces.IMerchantArmyOfPoachersIssueInterface"/>'s type doc comment.
///
/// Deliberately its own, independent postfix-only class rather than a change to
/// <see cref="IssueManagerCreateNewIssuePatches"/> - that type's Prefix (client-creation blocking) is already
/// fully generic, so it already covers this issue type's creation with zero changes; only a second postfix is
/// needed to also capture/broadcast THIS type's result.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class MerchantArmyOfPoachersIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue issue) return;

        MessageBroker.Instance.Publish(issueOwner, new MerchantArmyOfPoachersIssueCreated(issue));
    }
}
