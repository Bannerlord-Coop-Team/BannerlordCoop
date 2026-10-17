using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>Routes the three inline dialogue rewards before any personal or world consequence runs.</summary>
[HarmonyPatch]
internal class ExtortionByDesertersRewardPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.ApplyQuestResult))]
    [HarmonyPrefix]
    private static bool ApplyQuestResultPrefix(Quest __instance, ref Quest.ExtortionByDesertersQuestResult result)
    {
        if (ModInformation.IsServer) return true;
        if (!result.IsSuccess) return false;

        var branch = result.GoldMultiplier == 1f ? ExtortionByDesertersBranch.FullReward :
            result.GoldMultiplier == 0.5f ? ExtortionByDesertersBranch.HalfReward : ExtortionByDesertersBranch.RefuseReward;
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            dispatcher.BeforeQuestBranch(__instance, (byte)branch);
        }
        return false;
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog), new[] { typeof(TextObject), typeof(bool) })]
    [HarmonyPrefix]
    private static void AddLogPrefix(QuestBase __instance, TextObject text)
    {
        if (ModInformation.IsServer && __instance is Quest quest &&
            ContainerProvider.TryResolve<ExtortionByDesertersSync>(out var sync))
        {
            sync.Send(quest, text.ToString());
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool OnTimedOutPrefix() => ModInformation.IsServer;
}
