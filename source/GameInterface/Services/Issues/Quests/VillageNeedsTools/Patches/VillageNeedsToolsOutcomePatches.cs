using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsTools.Patches;

/// <summary>Routes delivery to the server and keeps replay from applying personal penalties twice.</summary>
[HarmonyPatch]
internal class VillageNeedsToolsOutcomePatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.FinishQuestSuccess1))]
    [HarmonyPrefix]
    private static bool FinishQuestSuccessPrefix(Quest __instance)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)
            || dispatcher.BeforeQuestBranch(__instance, (byte)VillageNeedsToolsBranch.DeliverTools);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFailed))]
    [HarmonyPrefix]
    private static bool PenaltyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix(Quest __instance)
    {
        if (ModInformation.IsServer) return true;

        __instance.AddLog(__instance.QuestTimeOutFailLog);
        return false;
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess))]
    [HarmonyPrefix]
    private static void SuccessLogPrefix(QuestBase __instance)
    {
        if (ModInformation.IsClient && CallOriginalPolicy.IsOriginalAllowed() && __instance is Quest quest)
        {
            quest.AddLog(quest.QuestSuccessLog);
        }
    }
}
