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
/// Same shape/reasoning as <see cref="IssueQuestAcceptancePatch"/> (see that type's doc comment for why
/// neither this nor the alternative-solution equivalent below is ever blocked - both run inline inside a live
/// one-to-one conversation), scoped to
/// <see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue"/>. Deliberately its
/// own, independent postfix-only class rather than a change to <see cref="IssueAcceptancePatches"/> - Harmony
/// runs multiple independent postfixes on the same vanilla method without conflict, and keeping this issue
/// type's accept-capture entirely separate avoids any coupling with Tools' file.
///
/// Additionally captures this accept's authoritative <c>_requestedItemAmount</c>/<c>RewardGold</c> - the
/// central mechanism this issue type needs (see
/// <see cref="Interfaces.IVillageNeedsCraftingMaterialsIssueInterface"/>'s type doc comment): unlike Tools,
/// these are re-derived per-client at accept time, so whichever machine's accept is genuine must capture what
/// it just baked, right here, for the server to arbitrate and broadcast.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class VillageNeedsCraftingMaterialsQuestAcceptancePatch
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue) return;

        if (!ContainerProvider.TryResolve<IVillageNeedsCraftingMaterialsIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var requestedItemAmount, out var rewardGold)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new VillageCraftingIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId, requestedItemAmount, rewardGold));
    }
}

/// <summary>
/// Same shape/reasoning as <see cref="IssueWithAlternativeSolutionAcceptancePatch"/>, scoped to
/// <see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue"/>. No
/// amount/reward capture needed here - see <see cref="VillageCraftingIssueAlternativeAcceptTriggered"/>'s doc
/// comment for why that path has no cross-peer divergence to fix.
/// </summary>
[HarmonyPatch(typeof(IssueBase))]
internal class VillageNeedsCraftingMaterialsAlternativeAcceptancePatch
{
    [HarmonyPatch(nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (__instance is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue villageIssue) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(villageIssue,
            new VillageCraftingIssueAlternativeAcceptTriggered(villageIssue.IssueOwner, controllerIdProvider?.ControllerId));
    }
}
