using Common;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory.Patches;

[HarmonyPatch]
internal sealed class RaidAnEnemyTerritoryBranchPatches
{
    private static readonly ConditionalWeakTable<Quest, object> HandedOff = new();

    [HarmonyPatch(typeof(Quest), "MainHeroRaidedAllVillages")]
    [HarmonyPrefix]
    private static bool MainHeroRaidedAllVillagesPrefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(__instance, (byte)RaidAnEnemyTerritoryBranch.SuccessfulRaid))
        {
            return true;
        }

        HandedOff.GetValue(__instance, _ => new object());
        return false;
    }

    [HarmonyPatch(typeof(FinalizationCoordinator), "Replay")]
    [HarmonyPrefix]
    private static void AddSuccessLogOnClientReplay(IssueBase issue, IssueOutcome outcome)
    {
        if (ModInformation.IsClient && outcome == IssueOutcome.QuestSuccess &&
            issue?.IssueQuest is Quest quest && quest.IsOngoing)
        {
            quest.AddLog(new TextObject("{=gMvDCnlx}You have successfully raided enemy villages and distracted enemy forces as promised."));
        }
    }

    [HarmonyPatch(typeof(Quest), "DailyTick")]
    [HarmonyPrefix]
    private static bool DailyTickPrefix(Quest __instance) => !HandedOff.TryGetValue(__instance, out _);

    [HarmonyPatch(typeof(Quest), "OnBeforeTimedOut")]
    [HarmonyPrefix]
    private static bool OnBeforeTimedOutPrefix(Quest __instance) => !HandedOff.TryGetValue(__instance, out _);

    [HarmonyPatch(typeof(Quest), "OnTimedOut")]
    [HarmonyPrefix]
    private static bool OnTimedOutPrefix(Quest __instance) => ModInformation.IsServer && !HandedOff.TryGetValue(__instance, out _);

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel))]
    [HarmonyPostfix]
    private static void CompleteCancelPostfix(QuestBase __instance)
    {
        if (ModInformation.IsClient && __instance is Quest quest && quest.IsOngoing)
        {
            HandedOff.GetValue(quest, _ => new object());
        }
    }
}
