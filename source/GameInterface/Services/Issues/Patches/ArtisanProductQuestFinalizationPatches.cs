using Common;
using Common.Util;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using System.Linq;

namespace GameInterface.Services.Issues.Patches;

using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

[HarmonyPatch]
internal sealed class ArtisanProductQuestFinalizationPatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithBetrayal));
    }

    [HarmonyPrefix]
    private static bool EnterOwner(QuestBase __instance, MethodBase __originalMethod, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Quest) return true;
        if (!__instance.IsOngoing) return false;
        if (ArtisanProductQuestLoadPatch.IsLoading &&
            __originalMethod.Name == nameof(QuestBase.CompleteQuestWithCancel) &&
            !Campaign.Current.IssueManager.Issues.Values.Any(issue => issue.IssueQuest == __instance)) return true;
        if (ArtisanProductPlayerChangePatch.ChangedPlayer != null && ModInformation.IsClient) return false;
        if (ModInformation.IsClient) return AllowedThread.IsThisThreadAllowed();
        return ContainerProvider.TryResolve<IArtisanProductAuthority>(out var authority) &&
            authority.TryEnter(__instance.QuestGiver, out __state, ArtisanProductPlayerChangePatch.ChangedPlayer);
    }

    [HarmonyFinalizer]
    private static void ExitOwner(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnGameLoaded))]
internal sealed class ArtisanProductQuestLoadPatch
{
    [ThreadStatic]
    private static int loading;

    internal static bool IsLoading => loading > 0;

    [HarmonyPrefix]
    private static void BeforeLoad() => loading++;

    [HarmonyFinalizer]
    private static void AfterLoad() => loading--;
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnPlayerCharacterChanged))]
internal sealed class ArtisanProductPlayerChangePatch
{
    [ThreadStatic]
    internal static Hero ChangedPlayer;

    [HarmonyPrefix]
    private static void BeforeChange(Hero newPlayer, out Hero __state)
    {
        __state = ChangedPlayer;
        ChangedPlayer = newPlayer;
    }

    [HarmonyFinalizer]
    private static void AfterChange(Hero __state) => ChangedPlayer = __state;
}
