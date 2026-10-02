using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

[HarmonyPatch]
internal static class HeadmanHerdDialogueOwnershipPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Quest), "<SetDialogs>b__31_0");
        yield return AccessTools.DeclaredMethod(typeof(Quest), "<SetDialogs>b__31_1");
        yield return AccessTools.DeclaredMethod(typeof(Quest), "<GetDeliveryDialogFlow>b__35_0");
    }

    [HarmonyPrefix]
    internal static bool Prefix(Quest __instance, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership)
            && ownership.IsLocalOwner(__instance.StringId)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch]
internal static class HeadmanHerdServerDialogPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.SetDialogs));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.InitializeQuestOnGameLoad));
    }

    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
}
