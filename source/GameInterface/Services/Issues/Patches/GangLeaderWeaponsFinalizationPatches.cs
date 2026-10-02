using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

[HarmonyPatch]
internal static class GangLeaderWeaponsFinalizationPatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut));
    }

    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Quest quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!quest.IsOngoing) return false;
        if (ModInformation.IsClient) return AllowedThread.IsThisThreadAllowed();
        if (GangLeaderWeaponsActionScope.Contains(quest)) return true;
        return ContainerProvider.TryResolve<IGangLeaderWeaponsOwnerContext>(out var context) && context.TryOpen(quest, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();

    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance)
    {
        if (__instance is Quest quest && !quest.IsOngoing)
            MessageBroker.Instance.Publish(quest, new GangLeaderWeaponsQuestEnded(quest));
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
internal static class GangLeaderWeaponsJournalPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, JournalLog __result, bool hideInformation)
    {
        if (ModInformation.IsServer && __instance is Quest quest && GangLeaderWeaponsActionScope.Contains(quest))
            MessageBroker.Instance.Publish(quest, new GangLeaderWeaponsLogAdded(quest, __result, hideInformation));
    }
}

[HarmonyPatch(typeof(Quest), "OnSettlementOwnerChanged")]
internal static class GangLeaderWeaponsSettlementOwnerPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return ModInformation.IsServer &&
            ContainerProvider.TryResolve<IGangLeaderWeaponsOwnerContext>(out var context) && context.TryOpen(__instance, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
