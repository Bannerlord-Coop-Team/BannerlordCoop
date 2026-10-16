using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyNeedsSupplies.Patches;

[HarmonyPatch]
internal class ArmyNeedsSuppliesBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.CollectedGrainConsequence))]
    [HarmonyPrefix]
    private static bool SuccessByGrainPrefix(Quest __instance)
    {
        return BeforeBranch(__instance, ArmyNeedsSuppliesBranch.SuccessByGrain);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.CollectedGrainAndLiveStockConsequence))]
    [HarmonyPrefix]
    private static bool SuccessByGrainAndLiveStockPrefix(Quest __instance)
    {
        return BeforeBranch(__instance, ArmyNeedsSuppliesBranch.SuccessByGrainAndLiveStock);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.CollectedGrainAndWineConsequence))]
    [HarmonyPrefix]
    private static bool SuccessByGrainAndWinePrefix(Quest __instance)
    {
        return BeforeBranch(__instance, ArmyNeedsSuppliesBranch.SuccessByGrainAndWine);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.CollectedEverythingConsequence))]
    [HarmonyPrefix]
    private static bool SuccessByEverythingPrefix(Quest __instance)
    {
        return BeforeBranch(__instance, ArmyNeedsSuppliesBranch.SuccessByEverything);
    }

    private static bool BeforeBranch(Quest quest, ArmyNeedsSuppliesBranch branch)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        if (dispatcher.BeforeQuestBranch(quest, (byte)branch))
        {
            return true;
        }

        MapEventHelper.OnConversationEnd();
        return false;
    }
}
