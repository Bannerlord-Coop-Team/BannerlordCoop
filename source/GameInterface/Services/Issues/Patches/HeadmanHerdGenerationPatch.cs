using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.CreateNewIssue))]
internal static class HeadmanHerdGenerationPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(in PotentialIssueData pid, Hero issueOwner, ref bool __result, out IDisposable __state)
    {
        __state = null;
        if (pid.IssueType != typeof(Issue) || ModInformation.IsClient
            || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ContainerProvider.TryResolve<IHeadmanHerdGenerationContext>(out var context)
            && context.TryEnter(issueOwner, out __state)) return true;
        __result = false;
        return false;
    }

    [HarmonyFinalizer]
    internal static void Finalizer(IDisposable __state) => __state?.Dispose();
}
