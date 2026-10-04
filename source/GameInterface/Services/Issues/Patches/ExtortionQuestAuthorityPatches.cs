using Common;
using Common.Util;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Generic;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

[HarmonyPatch]
internal class ExtortionQuestWorldCallbackPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.MapEventEnded));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnSettlementLeft));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnFinalize));
    }

    [HarmonyPrefix]
    private static bool Prefix(Quest __instance, MethodBase __originalMethod, out IDisposable __state)
    {
        __state = null;
        if (__originalMethod.Name == nameof(Quest.OnFinalize) && QuestSolutionStartAuthorityGuard.IsActive &&
            IssueFinalizeAuthorityGuard.IsActive) return true;
        return ModInformation.IsServer &&
            ContainerProvider.TryResolve<IExtortionQuestContext>(out var context) &&
            context.TryEnter(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(Quest __instance, IDisposable __state, Exception __exception)
    {
        try
        {
            if (__state != null && __exception == null)
                MessageBroker.Instance.Publish(__instance, new ExtortionQuestChanged(__instance));
        }
        finally
        {
            __state?.Dispose();
        }
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
internal class ExtortionQuestJournalPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance) => __instance is not Quest || ModInformation.IsServer ||
        AllowedThread.IsThisThreadAllowed();

    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, JournalLog __result)
    {
        if (ModInformation.IsServer && __instance is Quest quest && __result != null)
            MessageBroker.Instance.Publish(quest, new ExtortionQuestChanged(quest));
    }
}

[HarmonyPatch]
internal class ExtortionQuestCompletionPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithBetrayal));
    }

    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Quest) return true;
        if (ModInformation.IsClient)
            return AllowedThread.IsThisThreadAllowed() && IssueFinalizeAuthorityGuard.IsActive;
        if (QuestSolutionStartAuthorityGuard.IsActive && IssueFinalizeAuthorityGuard.IsActive) return true;
        return ContainerProvider.TryResolve<IExtortionQuestContext>(out var context) &&
            context.TryEnter(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
