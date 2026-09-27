using Common;
using Common.Messaging;
using Common.Logging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssueManager))]
internal class IssueManagerQuestCompletedReasonCapture
{
    public static readonly Dictionary<Hero, IssueFinalizeReason> PendingReasons = new();

    [HarmonyPatch(nameof(IssueManager.OnQuestCompleted))]
    [HarmonyPrefix]
    private static void Prefix(QuestBase quest, QuestBase.QuestCompleteDetails detail)
    {
        if (quest?.QuestGiver == null) return;
        if (!DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted(quest.QuestGiver.Issue)) return;
        if (PendingReasons.TryGetValue(quest.QuestGiver, out var pending) && pending == IssueFinalizeReason.RejectedAccept) return;

        PendingReasons[quest.QuestGiver] = detail switch
        {
            QuestBase.QuestCompleteDetails.Success => IssueFinalizeReason.QuestSuccess,
            QuestBase.QuestCompleteDetails.Cancel => IssueFinalizeReason.QuestCancel,
            QuestBase.QuestCompleteDetails.Fail => IssueFinalizeReason.QuestFail,
            QuestBase.QuestCompleteDetails.Timeout => IssueFinalizeReason.QuestTimeout,
            QuestBase.QuestCompleteDetails.FailWithBetrayal => IssueFinalizeReason.QuestBetrayal,
            _ => IssueFinalizeReason.IssueOnly,
        };
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class IssueFinalizedOwnershipGatePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueFinalizedOwnershipGatePatch>();

    [HarmonyPrefix]
    internal static bool Prefix(IssueBase __instance)
    {
        if (!DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted(__instance)) return true;

        var allowed = IssueFinalizeAuthorityGuard.IsActive || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
        if (ModInformation.IsServer)
            Logger.Information("Issue finalization for {Issue} allowed={Allowed} quest={Quest} stack={Stack}",
                __instance.StringId, allowed, __instance.IssueQuest?.StringId, Environment.StackTrace);
        return allowed;
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnQuestFinalized))]
internal class IssueQuestFinalizedTracePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueQuestFinalizedTracePatch>();

    [HarmonyPrefix]
    private static void Prefix(QuestBase quest)
    {
        if (ModInformation.IsClient ||
            quest is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest) return;
        Logger.Information("Quest manager finalizing {Quest} ongoing={Ongoing} issue={Issue} stack={Stack}",
            quest.StringId, quest.IsOngoing, quest.QuestGiver?.Issue?.StringId, Environment.StackTrace);
    }
}

[HarmonyPatch(typeof(IssueBase))]
internal class IssueExpiryFinalizeAuthorityPatch
{
    [HarmonyPatch(nameof(IssueBase.CompleteIssueWithTimedOut))]
    [HarmonyPrefix]
    private static void TimedOutPrefix(out IssueFinalizeAuthorityGuard __state)
    {
        __state = OpenGuardIfAuthoritative();
    }

    [HarmonyPatch(nameof(IssueBase.CompleteIssueWithTimedOut))]
    [HarmonyFinalizer]
    private static void TimedOutFinalizer(IssueFinalizeAuthorityGuard __state)
    {
        __state?.Dispose();
    }

    [HarmonyPatch(nameof(IssueBase.CompleteIssueWithStayAliveConditionsFailed))]
    [HarmonyPrefix]
    private static void StayAliveFailedPrefix(out IssueFinalizeAuthorityGuard __state)
    {
        __state = OpenGuardIfAuthoritative();
    }

    [HarmonyPatch(nameof(IssueBase.CompleteIssueWithStayAliveConditionsFailed))]
    [HarmonyFinalizer]
    private static void StayAliveFailedFinalizer(IssueFinalizeAuthorityGuard __state)
    {
        __state?.Dispose();
    }

    private static IssueFinalizeAuthorityGuard OpenGuardIfAuthoritative()
    {
        return CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer
            ? new IssueFinalizeAuthorityGuard()
            : null;
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class IssueFinalizedPatches
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        var owner = __instance.IssueOwner;
        var reason = IssueFinalizeReason.IssueOnly;
        if (owner != null && IssueManagerQuestCompletedReasonCapture.PendingReasons.TryGetValue(owner, out var pending))
        {
            reason = pending;
            IssueManagerQuestCompletedReasonCapture.PendingReasons.Remove(owner);
        }

        var wasGenuinelyFinalized = !DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted(__instance) || IssueFinalizeAuthorityGuard.IsActive;
        if (wasGenuinelyFinalized)
        {
            if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry)) ownershipRegistry.Clear(owner);

            if (owner != null &&
                ContainerProvider.TryResolve<IObjectManager>(out var objectManager) &&
                ContainerProvider.TryResolve<IIssueConversationTracker>(out var conversationTracker) &&
                objectManager.TryGetIdWithLogging(owner, out var ownerId))
            {
                conversationTracker.Clear(ownerId);
            }
        }

        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted(__instance)) return;

        MessageBroker.Instance.Publish(__instance, new IssueFinalizedTriggered(owner, reason));
    }
}
