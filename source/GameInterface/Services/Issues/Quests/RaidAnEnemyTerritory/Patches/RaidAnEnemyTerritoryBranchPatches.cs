using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal sealed class RaidAnEnemyTerritoryBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.MainHeroRaidedAllVillages))]
    [HarmonyPrefix]
    private static bool MainHeroRaidedAllVillagesPrefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(__instance, (byte)RaidAnEnemyTerritoryBranch.SuccessfulRaid))
        {
            return true;
        }

        return false;
    }
}
