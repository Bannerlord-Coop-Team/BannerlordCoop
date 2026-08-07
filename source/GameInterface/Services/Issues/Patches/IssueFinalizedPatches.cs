using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
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

        IssueOwnershipRegistry.Clear(owner);

        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted(__instance)) return;

        MessageBroker.Instance.Publish(__instance, new IssueFinalizedTriggered(owner, reason));
    }
}
