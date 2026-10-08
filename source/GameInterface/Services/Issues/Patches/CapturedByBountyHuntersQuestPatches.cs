using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.CapturedByBountyHunters;
using GameInterface.Services.MobileParties.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Patches;

using Quest = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

[HarmonyPatch]
internal class CapturedByBountyHuntersQuestEventPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnMapEventEnded));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnSessionLaunched));
    }

    [HarmonyPrefix]
    private static bool BeginOwnerEvent(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return false;
        return ContainerProvider.TryResolve<IBountyHuntersQuestContext>(out var context)
            && context.TryOpen(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void EndOwnerEvent(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(Quest))]
internal class CapturedByBountyHuntersQuestStartAndTimeoutPatches
{
    [HarmonyPatch(nameof(Quest.OnSettlementLeft))]
    [HarmonyPrefix]
    private static bool RevealForOwner(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient)
            return ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var owners) && owners.IsLocalPeerOwner(__instance.QuestGiver);
        return ContainerProvider.TryResolve<IBountyHuntersQuestContext>(out var context)
            && context.TryOpen(__instance.QuestGiver, out __state);
    }

    [HarmonyPatch(nameof(Quest.OnSettlementLeft))]
    [HarmonyFinalizer]
    private static void EndReveal(IDisposable __state) => __state?.Dispose();

    [HarmonyPatch(nameof(Quest.QuestAcceptedConsequences))]
    [HarmonyPrefix]
    private static bool AcceptOnce(Quest __instance) => __instance.JournalEntries.Count == 0;

    [HarmonyPatch(nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool ApplyTimeoutConsequences() =>
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;
}

[HarmonyPatch]
internal class CapturedByBountyHuntersQuestTerminalPatches
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
    private static bool BeginTerminal(QuestBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Quest) return true;
        if (ModInformation.IsClient && LeaveSettlementActionPatches.SuppressForPlayerSwitch) return false;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!__instance.IsOngoing) return false;
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive;
        return ContainerProvider.TryResolve<IBountyHuntersQuestContext>(out var context)
            && context.TryOpen(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void EndTerminal(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(TraitLevelingHelper), nameof(TraitLevelingHelper.AddTraitXp))]
internal class CapturedByBountyHuntersTraitXpPatch
{
    [HarmonyPrefix]
    private static bool ApplyOwnerXp(TraitObject trait, int xpAmount)
    {
        var owner = BountyHuntersOwnerScope.CurrentOwner;
        if (owner == null) return true;
        if (ContainerProvider.TryResolve<IOwnerTraitXpProgress>(out var progress)) progress.Apply(owner, trait, xpAmount);
        return false;
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class CapturedByBountyHuntersAlternativeCompletionPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    private static bool Begin(IssueBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue) return true;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return false;
        if (!__instance.IsSolvingWithAlternative || !__instance.AlternativeSolutionReturnTimeForTroops.IsPast) return false;
        return ContainerProvider.TryResolve<IBountyHuntersQuestContext>(out var context)
            && context.TryOpen(__instance.IssueOwner, out __state);
    }

    [HarmonyFinalizer]
    private static void End(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch]
internal class CapturedByBountyHuntersIssueCompletionPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAiLord));
    }

    [HarmonyPrefix]
    private static bool Begin(IssueBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue) return true;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive;
        if (__instance.IsOngoingWithoutQuest)
        {
            __state = new IssueFinalizeAuthorityGuard();
            return true;
        }
        return ContainerProvider.TryResolve<IBountyHuntersQuestContext>(out var context)
            && context.TryOpen(__instance.IssueOwner, out __state);
    }

    [HarmonyFinalizer]
    private static void End(IDisposable __state) => __state?.Dispose();
}
