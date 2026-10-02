using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class ExtortionAlternativeProgressPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueManager __instance)
    {
        if (ModInformation.IsClient) return;
        foreach (var entry in __instance.Issues)
            if (entry.Value is Issue issue && issue.IsSolvingWithAlternative)
                MessageBroker.Instance.Publish(issue, new ExtortionAlternativeChanged(issue));
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.AddLog))]
internal class ExtortionAlternativeJournalPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance is Issue issue)
            MessageBroker.Instance.Publish(issue, new ExtortionAlternativeChanged(issue));
    }
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueUpdated))]
internal class ExtortionAlternativeStatusPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (ModInformation.IsServer && issue is Issue extortion)
            MessageBroker.Instance.Publish(issue, new ExtortionAlternativeChanged(extortion, details));
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
internal class ExtortionIssueCancellationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Issue) return true;
        if (ModInformation.IsClient) return AllowedThread.IsThisThreadAllowed() && IssueFinalizeAuthorityGuard.IsActive;
        if (__instance.IsOngoingWithoutQuest || QuestSolutionStartAuthorityGuard.IsActive)
        {
            __state = new IssueFinalizeAuthorityGuard();
            return true;
        }
        return ContainerProvider.TryResolve<IExtortionQuestContext>(out var context) &&
            context.TryEnter(__instance.IssueOwner, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
