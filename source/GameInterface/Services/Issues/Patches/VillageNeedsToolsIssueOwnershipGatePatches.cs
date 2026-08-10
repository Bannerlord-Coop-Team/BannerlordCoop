using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest), "PlayerHasTools")]
internal class VillageNeedsToolsQuestOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest __instance, ref bool __result)
    {
        if (!IssueOwnershipRegistry.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = false;
            return false;
        }

        return true;
    }
}
