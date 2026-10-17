using Common;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
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
        ApplyPenalty(__instance);
        return true;
    }

    [HarmonyPatch(typeof(QuestHelper), nameof(QuestHelper.ApplyGenericMinorMajorCoercionConsequences))]
    [HarmonyPrefix]
    private static void CoercionPrefix(QuestBase quest)
    {
        if (ModInformation.IsServer && quest is Quest) ApplyPenalty(quest);
    }

    private static void ApplyPenalty(QuestBase quest)
    {
        if (ContainerProvider.TryResolve<ILadysKnightOutHonorProgress>(out var honor)) honor.ApplyPenalty(quest);
    }
}
