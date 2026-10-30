using Common;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Handlers;
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
internal class ExtortionByDesertersRewardPatches
{
    internal static bool ApplyQuestResultPrefix() => ModInformation.IsServer;

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
        return ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) &&
            dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog), new[] { typeof(TextObject), typeof(bool) })]
    [HarmonyPrefix]
    private static void AddLogPrefix(QuestBase __instance, TextObject text)
    {
        if (ModInformation.IsServer && __instance is Quest quest &&
            ContainerProvider.TryResolve<ExtortionByDesertersHandler>(out var sync))
        {
            sync.Send(quest, text);
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool OnTimedOutPrefix() => ModInformation.IsServer;
}
