using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts the result of a genuine server-side <c>IssueManager.CreateNewIssue</c> creating a
/// <see cref="GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue"/>. Needed even though
/// nothing per-instance is rolled/picked at creation time (see
/// <see cref="Interfaces.IGangLeaderNeedsWeaponsIssueInterface"/>'s type doc comment - the ctor takes ONLY the
/// owner, everything else is deterministic given shared state) - without this, a client would never see this
/// NPC as having an issue at all, since <see cref="IssueManagerCreateNewIssuePatches"/>'s own generic Prefix
/// already blocks EVERY client-originated <c>CreateNewIssue</c> call unconditionally, and its own Postfix only
/// broadcasts for <c>VillageNeedsToolsIssue</c>.
///
/// Deliberately its own, independent postfix-only class rather than a change to
/// <see cref="IssueManagerCreateNewIssuePatches"/> - same precedent as
/// <see cref="CaravanAmbushIssueCreationPatch"/>/<see cref="SmugglersIssueCreationPatch"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class GangLeaderNeedsWeaponsIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue issue) return;

        MessageBroker.Instance.Publish(issueOwner, new GangLeaderNeedsWeaponsIssueCreated(issue));
    }
}
