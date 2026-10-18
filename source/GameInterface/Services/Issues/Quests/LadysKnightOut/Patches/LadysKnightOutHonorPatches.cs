using Common;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut.Patches;

[HarmonyPatch]
internal class LadysKnightOutHonorPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.CriminalActionPerformedTowardsSettlement))]
    [HarmonyPrefix]
    private static bool CrimePrefix(Quest __instance)
    {
        if (ModInformation.IsClient) return false;
        if (ContainerProvider.TryResolve<ILadysKnightOutHonorProgress>(out var honor)) honor.ApplyPenalty(__instance);
        return true;
    }
}
