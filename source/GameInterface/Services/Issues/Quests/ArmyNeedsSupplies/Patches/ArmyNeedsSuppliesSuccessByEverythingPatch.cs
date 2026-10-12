using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyNeedsSupplies.Patches;

[HarmonyPatch(typeof(Quest), nameof(Quest.CollectedEverythingConsequence))]
internal class ArmyNeedsSuppliesSuccessByEverythingPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        if (dispatcher.BeforeQuestBranch(__instance, (byte)ArmyNeedsSuppliesBranch.SuccessByEverything))
        {
            return true;
        }

        MapEventHelper.OnConversationEnd();
        return false;
    }
}
