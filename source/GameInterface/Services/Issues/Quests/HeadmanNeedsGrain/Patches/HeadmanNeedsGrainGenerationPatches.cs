using Common;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Behavior = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class HeadmanNeedsGrainGenerationPatches
{
    [HarmonyPatch(typeof(Behavior), nameof(Behavior.OnCheckForIssue))]
    [HarmonyPrefix]
    private static bool OnCheckPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAiLord))]
    [HarmonyPrefix]
    private static bool AiCompletionPrefix(IssueBase __instance, Hero issueSolver)
    {
        if (!(__instance is Issue)) return true;
        if (ModInformation.IsClient && CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer && !issueSolver.IsPlayerHero();
    }
}
