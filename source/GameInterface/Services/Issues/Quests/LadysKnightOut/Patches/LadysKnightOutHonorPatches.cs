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
    private static bool CrimePrefix(out int __state)
    {
        __state = ModInformation.IsClient || Campaign.Current?.PlayerTraitDeveloper == null
            ? 0
            : Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor);
        return true;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.CriminalActionPerformedTowardsSettlement))]
    [HarmonyPostfix]
    private static void CrimePostfix(Quest __instance, int __state)
    {
        if (ModInformation.IsClient) return;
        Campaign.Current.PlayerTraitDeveloper?.SetPropertyValue(DefaultTraits.Honor, __state);
        if (ContainerProvider.TryResolve<ILadysKnightOutHonorProgress>(out var honor)) honor.ApplyPenalty(__instance);
    }
}

[HarmonyPatch]
internal class LadysKnightOutHonorMirrorPatches
{
    [HarmonyPatch(typeof(PropertyOwner<PropertyObject>), nameof(PropertyOwner<PropertyObject>.SetPropertyValue))]
    [HarmonyPostfix]
    private static void MirrorClientHonor(PropertyOwner<PropertyObject> __instance, PropertyObject attribute, int value)
    {
        if (ModInformation.IsServer || AllowedThread.IsThisThreadAllowed() || Campaign.Current?.PlayerTraitDeveloper != __instance ||
            attribute != DefaultTraits.Honor) return;
        if (ContainerProvider.TryResolve<ILadysKnightOutHonorProgress>(out var honor)) honor.MirrorClientHonor(value);
    }
}
