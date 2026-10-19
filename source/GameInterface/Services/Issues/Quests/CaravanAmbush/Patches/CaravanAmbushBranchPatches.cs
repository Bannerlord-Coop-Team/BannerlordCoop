using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatch]
internal class CaravanAmbushBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnQuestSucceeded))]
    [HarmonyPrefix]
    private static bool Reward(Quest __instance)
    {
        if (!__instance.IsOngoing) return false;
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) return true;
        return dispatcher.BeforeQuestBranch(__instance, (byte)CaravanAmbushBranch.CollectReward);
    }
}
