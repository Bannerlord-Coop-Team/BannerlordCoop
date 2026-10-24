using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
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
