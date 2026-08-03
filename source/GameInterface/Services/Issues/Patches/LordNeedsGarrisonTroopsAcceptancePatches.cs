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
/// Captures this accept's authoritative <c>_requestedTroopAmount</c>/<c>_rewardGold</c> - the survey's flagged
/// live <c>PartyWageModel</c> read (plus the <c>IssueDifficultyMultiplier</c>-derived troop count it missed) -
/// see <see cref="ILordNeedsGarrisonTroopsIssueInterface"/>'s doc comment. Own independent postfix, same shape
/// as <see cref="VillageNeedsCraftingMaterialsQuestAcceptancePatch"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class LordNeedsGarrisonTroopsAcceptancePatches
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue) return;

        if (!ContainerProvider.TryResolve<ILordNeedsGarrisonTroopsIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var requestedTroopAmount, out var rewardGold)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new LordNeedsGarrisonTroopsIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId, requestedTroopAmount, rewardGold));
    }
}
