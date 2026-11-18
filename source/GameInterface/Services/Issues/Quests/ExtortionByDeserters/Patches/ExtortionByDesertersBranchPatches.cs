using Common;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>Routes the three inline dialogue rewards before any personal or world consequence runs.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ExtortionByDesertersBranchPatches
{
    [HarmonyPatch]
    [HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
    private static class FullRewardBranchPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(Quest), "<get_QuestCompletionDialogFlow>b__57_1");

        [HarmonyPrefix]
        private static bool Prefix(Quest __instance)
        {
            return BeforeBranch(__instance, ExtortionByDesertersBranch.FullReward);
        }
    }

    [HarmonyPatch]
    [HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
    private static class HalfRewardBranchPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(Quest), "<get_QuestCompletionDialogFlow>b__57_3");

        [HarmonyPrefix]
        private static bool Prefix(Quest __instance)
        {
            return BeforeBranch(__instance, ExtortionByDesertersBranch.HalfReward);
        }
    }

    [HarmonyPatch]
    [HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
    private static class RefuseRewardBranchPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(Quest), "<get_QuestCompletionDialogFlow>b__57_5");

        [HarmonyPrefix]
        private static bool Prefix(Quest __instance)
        {
            return BeforeBranch(__instance, ExtortionByDesertersBranch.RefuseReward);
        }
    }

    private static bool BeforeBranch(Quest quest, ExtortionByDesertersBranch branch)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }

}
