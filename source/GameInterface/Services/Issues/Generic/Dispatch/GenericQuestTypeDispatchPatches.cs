using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Dispatch;

[HarmonyPatch(typeof(IssueManager))]
internal class GenericQuestTypeCreationTriggerPatch
{
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

[HarmonyPatch(typeof(IssueBase))]
internal class GenericQuestTypeQuestSolutionAcceptTriggerPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(nameof(IssueBase.StartIssueWithQuest))]
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || IssueDispatchReplayGuard.IsActive) return;
        if (QuestSolutionStartAuthorityGuard.IsActive) return;

        var issueOwner = __instance.IssueOwner;
        var descriptor = QuestTypeRegistry.Get(__instance);
        if (descriptor?.SupportsQuestSolutionAccept != true) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        descriptor.OnGenuineQuestSolutionAccept?.Invoke(issueOwner, controllerIdProvider?.ControllerId);
        MessageBroker.Instance.Publish(issueOwner, new QuestTypeQuestSolutionAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId));
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithQuest))]
internal class GenericQuestTypeQuestSolutionStartOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, ref bool __result)
    {
        if (QuestTypeRegistry.Get(__instance)?.SupportsQuestSolutionAccept != true) return true;
        if (QuestSolutionStartAuthorityGuard.IsActive) return true;

        __result = true;
        return false;
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
        if (AlternativeSolutionStartAuthorityGuard.IsActive) return;

        var descriptor = QuestTypeRegistry.Get(__instance);
        if (descriptor?.SupportsAlternativeAccept != true) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        descriptor.OnGenuineAlternativeAccept?.Invoke(__instance.IssueOwner, controllerIdProvider?.ControllerId);
        MessageBroker.Instance.Publish(__instance, new QuestTypeAlternativeAcceptTriggered(__instance.IssueOwner, controllerIdProvider?.ControllerId));
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class GenericQuestTypeAlternativeSolutionOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (QuestTypeRegistry.Get(__instance)?.SupportsAlternativeAccept != true) return true;

        return (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) && ownershipRegistry.IsLocalPeerOwner(__instance.IssueOwner))
            || AlternativeSolutionCompletionAuthorityGuard.IsActive;
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
internal class GenericQuestTypeAlternativeSolutionStartOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (QuestTypeRegistry.Get(__instance)?.SupportsAlternativeAccept != true) return true;

        return AlternativeSolutionStartAuthorityGuard.IsActive;
    }
}
