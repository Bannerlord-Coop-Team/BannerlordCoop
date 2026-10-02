using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using Common.Network;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

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
    private static bool Prefix(QuestBase __instance, out (IDisposable Scope, bool WasOrphan) __state)
    {
        __state = default;
        if (__instance is not Quest quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!quest.IsOngoing) return false;
        if (ModInformation.IsClient)
        {
            if (!AllowedThread.IsThisThreadAllowed()) return false;
            __state = (new GangLeaderWeaponsActionScope(quest), false);
            return true;
        }
        IDisposable scope = null;
        if (!GangLeaderWeaponsActionScope.Contains(quest) &&
            (!ContainerProvider.TryResolve<IGangLeaderWeaponsOwnerContext>(out var context) || !context.TryOpen(quest, out scope))) return false;
        __state = (scope, !Campaign.Current.IssueManager.Issues.TryGetValue(quest.QuestGiver, out var issue) || issue?.IssueQuest != quest);
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer((IDisposable Scope, bool WasOrphan) __state) => __state.Scope?.Dispose();

    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, MethodBase __originalMethod, (IDisposable Scope, bool WasOrphan) __state)
    {
        if (__instance is Quest quest && !quest.IsOngoing)
        {
            MessageBroker.Instance.Publish(quest, new GangLeaderWeaponsQuestEnded(quest));
            // Loaded orphan quests have no IssueManager callback to announce their cancellation.
            if (ModInformation.IsServer && __state.WasOrphan &&
                ContainerProvider.TryResolve<IObjectManager>(out var objects) &&
                objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId) &&
                ContainerProvider.TryResolve<IIssueGenerationRegistry>(out var generations) &&
                generations.TryGetGeneration(quest.QuestGiver, out var generation) &&
                ContainerProvider.TryResolve<INetwork>(out var network))
            {
                var reason = __originalMethod.Name switch
                {
                    nameof(QuestBase.CompleteQuestWithSuccess) => IssueFinalizeReason.QuestSuccess,
                    nameof(QuestBase.CompleteQuestWithFail) => IssueFinalizeReason.QuestFail,
                    nameof(QuestBase.CompleteQuestWithTimeOut) => IssueFinalizeReason.QuestTimeout,
                    _ => IssueFinalizeReason.QuestCancel,
                };
                network.SendAll(new NetworkIssueRemoved(giverId, reason,
                    questId: quest.StringId, generation: generation));
                IssueManagerQuestCompletedReasonCapture.PendingReasons.Remove(quest.QuestGiver);
                if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership)) ownership.Clear(quest.QuestGiver);
            }
        }
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

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnPlayerCharacterChanged))]
internal static class GangLeaderWeaponsPlayerChangedPatch
{
    [HarmonyPrefix]
    private static void Prefix(Hero oldPlayer, Hero newPlayer, MobileParty newPlayerParty,
        out GangLeaderWeaponsPlayerChangeScope __state)
    {
        __state = new GangLeaderWeaponsPlayerChangeScope(oldPlayer, newPlayer, newPlayerParty);
    }

    [HarmonyFinalizer]
    private static void Finalizer(GangLeaderWeaponsPlayerChangeScope __state) => __state?.Dispose();
}
