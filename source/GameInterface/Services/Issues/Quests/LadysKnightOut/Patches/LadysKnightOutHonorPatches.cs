using Common;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut.Patches;

[HarmonyPatch]
internal class LadysKnightOutHonorPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.CriminalActionPerformedTowardsSettlement))]
    [HarmonyPrefix]
    private static bool CrimePrefix() => ModInformation.IsServer;
}
