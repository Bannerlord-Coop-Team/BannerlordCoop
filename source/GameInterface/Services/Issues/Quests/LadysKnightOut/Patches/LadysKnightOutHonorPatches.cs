using Common;
using Common.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
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
    [HarmonyPatch(typeof(PropertyOwner<TraitObject>), nameof(PropertyOwner<TraitObject>.SetPropertyValue))]
    [HarmonyPostfix]
    private static void MirrorClientHonor(PropertyOwner<TraitObject> __instance, TraitObject attribute, int value)
    {
        if (ModInformation.IsServer || AllowedThread.IsThisThreadAllowed() || Campaign.Current?.PlayerTraitDeveloper != __instance ||
            attribute != DefaultTraits.Honor) return;
        if (ContainerProvider.TryResolve<ILadysKnightOutHonorProgress>(out var honor)) honor.MirrorClientHonor(value);
    }
}
