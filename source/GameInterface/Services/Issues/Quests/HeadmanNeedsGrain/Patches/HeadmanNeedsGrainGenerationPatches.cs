using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Issues.Framework.Finalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Behavior = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch]
internal class HeadmanNeedsGrainGenerationPatches
{
    [HarmonyPatch(typeof(Behavior), nameof(Behavior.OnCheckForIssue))]
    [HarmonyPrefix]
    private static bool OnCheckPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAiLord))]
    [HarmonyPrefix]
    private static bool AiCompletionPrefix(IssueBase __instance, Hero issueSolver, out bool __state)
    {
        __state = __instance is Issue;
        if (!__state) return true;
        __state = ModInformation.IsServer && !issueSolver.IsPlayerHero();
        return __state;
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAiLord))]
    [HarmonyPostfix]
    private static void AiCompletionPostfix(IssueBase __instance, bool __state)
    {
        if (__state) MessageBroker.Instance.Publish(__instance, new IssueOutcomeObserved(__instance, IssueOutcome.IssueStayAliveFailed));
    }
}
