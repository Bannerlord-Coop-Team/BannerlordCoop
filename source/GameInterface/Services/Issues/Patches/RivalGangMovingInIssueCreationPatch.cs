using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using SandBox.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts a genuine server-side creation of a
/// <see cref="RivalGangMovingInIssueBehavior.RivalGangMovingInIssue"/> - see
/// <see cref="Interfaces.IRivalGangMovingInIssueInterface"/>'s doc comment for why this is needed even though
/// <c>GetRivalGangLeader</c>'s own walk has no <c>MBRandom</c> roll in it (verify-per-type, don't assume,
/// matching this project's own established methodology). Own independent postfix, same shape as
/// <see cref="NearbyBanditBaseIssueCreationPatch"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class RivalGangMovingInIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not RivalGangMovingInIssueBehavior.RivalGangMovingInIssue rivalGangIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new RivalGangMovingInIssueCreated(rivalGangIssue));
    }
}
