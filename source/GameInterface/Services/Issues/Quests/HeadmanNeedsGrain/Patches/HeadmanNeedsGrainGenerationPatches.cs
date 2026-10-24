using Common;
using Common.Network;
using GameInterface.Policies;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Heroes.Extensions;
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
        if (ModInformation.IsClient && CallOriginalPolicy.IsOriginalAllowed())
        {
            __state = false;
            return true;
        }
        __state = ModInformation.IsServer && !issueSolver.IsPlayerHero();
        return __state;
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAiLord))]
    [HarmonyPostfix]
    private static void AiCompletionPostfix(IssueBase __instance, Hero issueSolver, bool __state)
    {
        if (!__state || !ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
            !objects.TryGetIdWithLogging(__instance.IssueOwner, out var giverId) ||
            !objects.TryGetIdWithLogging(issueSolver, out var solverId) ||
            !ContainerProvider.TryResolve<INetwork>(out var network)) return;
        network.SendAll(new NetworkHeadmanNeedsGrainAiCompleted(giverId, __instance.StringId, solverId));
    }
}
