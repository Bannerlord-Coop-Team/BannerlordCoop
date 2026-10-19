using Common;
using Common.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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

[HarmonyPatch]
internal class LadysKnightOutHonorMirrorPatches
{
    [HarmonyPatch(typeof(TraitLevelingHelper), "AddTraitXp")]
    [HarmonyPostfix]
    private static void MirrorClientHonor(TraitObject trait)
    {
        if (ModInformation.IsServer || AllowedThread.IsThisThreadAllowed() || trait != DefaultTraits.Honor ||
            Campaign.Current?.PlayerTraitDeveloper == null) return;
        if (ContainerProvider.TryResolve<ILadysKnightOutHonorProgress>(out var honor))
            honor.MirrorClientHonor(Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
    }
}
