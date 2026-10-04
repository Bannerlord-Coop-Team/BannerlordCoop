using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

[HarmonyPatch]
internal static class HeadmanHerdWorldEventPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnHeroKilled));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnWarDeclared));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnMapEventStarted));
    }

    [HarmonyPrefix]
    internal static bool Prefix(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return false;
        return ContainerProvider.TryResolve<IHeadmanHerdQuestAuthority>(out var authority)
            && authority.TryEnter(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_GAME_STARTED_CATEGORY)]
internal static class HeadmanHerdTerminalAuthorityPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess));
        yield return AccessTools.DeclaredMethod(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail));
        yield return AccessTools.DeclaredMethod(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut));
        yield return AccessTools.DeclaredMethod(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel));
    }

    [HarmonyPrefix]
    internal static bool Prefix(QuestBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive;
        return ContainerProvider.TryResolve<IHeadmanHerdQuestAuthority>(out var authority)
            && authority.TryEnter(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}


[HarmonyPatch(typeof(Quest), nameof(Quest.OnCanceled))]
[HarmonyPatchCategory(GameInterface.HARMONY_GAME_STARTED_CATEGORY)]
internal static class HeadmanHerdRemovalCancellationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance)
        => !HeadmanHerdQuestAuthority.CancelOrphanedHerd(__instance);
}
