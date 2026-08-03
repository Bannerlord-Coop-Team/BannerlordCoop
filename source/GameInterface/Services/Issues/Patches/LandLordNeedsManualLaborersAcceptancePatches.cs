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
/// Captures this accept's authoritative <c>_requestedPrisonerCount</c>/<c>_maximumPrisonerCount</c> - see
/// <see cref="ILandLordNeedsManualLaborersIssueInterface"/>'s doc comment. Own independent postfix, same shape
/// as <see cref="GangLeaderNeedsRecruitsAcceptancePatches"/>/<see cref="VillageNeedsCraftingMaterialsQuestAcceptancePatch"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class LandLordNeedsManualLaborersAcceptancePatches
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue) return;

        if (!ContainerProvider.TryResolve<ILandLordNeedsManualLaborersIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var requestedPrisonerCount, out var maximumPrisonerCount)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new LandLordManualLaborersIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId, requestedPrisonerCount, maximumPrisonerCount));
    }
}
