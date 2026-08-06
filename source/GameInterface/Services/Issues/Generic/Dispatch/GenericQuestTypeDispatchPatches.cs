using Common;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Dispatch;

[HarmonyPatch(typeof(IssueManager))]
internal class GenericQuestTypeCreationTriggerPatch
{
    // HarmonyPriority(First): this dispatch must publish before another postfix on the same method recurses
    // through network.SendAll into another peer's own MessageBroker context.
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;

        var issue = issueOwner?.Issue;
        var descriptor = QuestTypeRegistry.Get(issue);
        descriptor?.OnGenuineCreation?.Invoke(issue);
    }
}

[HarmonyPatch(typeof(IssueManager))]
internal class GenericQuestTypeQuestSolutionAcceptTriggerPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        // A mirror replay runs under AllowedThread - skip re-publishing so it doesn't loop back through the
        // network again. A genuine server-side replay only runs under IssueDispatchReplayGuard, so other
        // unrelated patches still fire normally.
        if (CallOriginalPolicy.IsOriginalAllowed() || IssueDispatchReplayGuard.IsActive) return;
        if (!__result) return;

        var descriptor = QuestTypeRegistry.Get(issueOwner?.Issue);
        if (descriptor?.OnGenuineQuestSolutionAccept == null) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        descriptor.OnGenuineQuestSolutionAccept(issueOwner, controllerIdProvider?.ControllerId);
    }
}

[HarmonyPatch(typeof(IssueBase))]
internal class GenericQuestTypeAlternativeAcceptTriggerPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || IssueDispatchReplayGuard.IsActive) return;

        var descriptor = QuestTypeRegistry.Get(__instance);
        if (descriptor?.OnGenuineAlternativeAccept == null) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        descriptor.OnGenuineAlternativeAccept(__instance.IssueOwner, controllerIdProvider?.ControllerId);
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class GenericQuestTypeAlternativeSolutionOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (!QuestTypeRegistry.IsRegistered(__instance?.GetType())) return true;

        return VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.IssueOwner)
            || AlternativeSolutionCompletionAuthorityGuard.IsActive;
    }
}
