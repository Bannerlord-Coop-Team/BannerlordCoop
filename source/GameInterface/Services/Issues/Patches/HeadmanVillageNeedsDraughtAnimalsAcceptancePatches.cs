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
/// Captures this accept's authoritative <c>_discountValue</c> - the survey's flagged roll (an unseeded
/// <c>MBRandom.RandomFloat</c> in the Quest's own ctor, i.e. at ACCEPT time) - see
/// <see cref="IHeadmanVillageNeedsDraughtAnimalsIssueInterface"/>'s doc comment. Own independent postfix, same
/// shape as <see cref="GangLeaderNeedsRecruitsAcceptancePatches"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class HeadmanVillageNeedsDraughtAnimalsAcceptancePatches
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue) return;

        if (!ContainerProvider.TryResolve<IHeadmanVillageNeedsDraughtAnimalsIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var discountValue)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new HeadmanDraughtAnimalsIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId, discountValue));
    }
}
