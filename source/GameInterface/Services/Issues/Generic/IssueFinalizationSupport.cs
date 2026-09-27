using Common.Util;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic;

public sealed class IssueFinalizeAuthorityGuard : IDisposable
{
    [ThreadStatic]
    private static int _count;

    public IssueFinalizeAuthorityGuard() => _count++;

    public void Dispose() => _count = _count > 0 ? _count - 1 : 0;

    public static bool IsActive => _count > 0;
}

internal static class IssueFinalizationSupport
{
    public static void FinalizeMirror(Hero owner, IssueFinalizeReason reason, bool suppressReplicationPatches = true, bool skipConsequenceReapplication = false)
    {
        if (owner?.Issue == null) return;

        if (reason == IssueFinalizeReason.RejectedAccept)
            IssueManagerQuestCompletedReasonCapture.PendingReasons[owner] = reason;
        IDisposable replicationScope = suppressReplicationPatches ? new AllowedThread() : null;
        try
        {
            using (new IssueFinalizeAuthorityGuard())
            using (replicationScope)
            {
                var quest = owner.Issue.IssueQuest;
                if (quest != null && quest.IsOngoing)
                {
                    ApplyOngoingQuestFinalize(owner, quest, reason, skipConsequenceReapplication);
                    return;
                }

                if (reason == IssueFinalizeReason.RejectedAccept)
                {
                    if (quest != null)
                        quest.CompleteQuestWithCancel();
                    else
                        owner.Issue.CompleteIssueWithCancel();
                    return;
                }

                owner.Issue.IssueFinalized();
            }
        }
        finally
        {
            if (reason == IssueFinalizeReason.RejectedAccept)
                IssueManagerQuestCompletedReasonCapture.PendingReasons.Remove(owner);
        }
    }

    private static void ApplyOngoingQuestFinalize(Hero owner, QuestBase quest, IssueFinalizeReason reason, bool skipConsequenceReapplication)
    {
        switch (reason)
        {
            case IssueFinalizeReason.QuestSuccess:
                var applyConsequence = skipConsequenceReapplication ? null : QuestTypeRegistry.Get(owner.Issue)?.ApplyQuestSuccessConsequence;
                RunConsequenceOrFallback(applyConsequence, quest, quest.CompleteQuestWithSuccess);
                return;
            case IssueFinalizeReason.QuestCancel:
                var applyCancelConsequence = skipConsequenceReapplication ? null : QuestTypeRegistry.Get(owner.Issue)?.ApplyQuestCancelConsequence;
                RunConsequenceOrFallback(applyCancelConsequence, quest, () => quest.CompleteQuestWithCancel());
                return;
            case IssueFinalizeReason.QuestFail:
                var applyFailConsequence = skipConsequenceReapplication ? null : QuestTypeRegistry.Get(owner.Issue)?.ApplyQuestFailConsequence;
                RunConsequenceOrFallback(applyFailConsequence, quest, () => quest.CompleteQuestWithFail());
                return;
            case IssueFinalizeReason.QuestTimeout:
                quest.CompleteQuestWithTimeOut();
                return;
            case IssueFinalizeReason.QuestBetrayal:
                var applyBetrayalConsequence = skipConsequenceReapplication ? null : QuestTypeRegistry.Get(owner.Issue)?.ApplyQuestBetrayalConsequence;
                RunConsequenceOrFallback(applyBetrayalConsequence, quest, () => quest.CompleteQuestWithBetrayal());
                return;
            default:
                quest.CompleteQuestWithCancel();
                return;
        }
    }

    private static void RunConsequenceOrFallback(Action<QuestBase> consequence, QuestBase quest, Action fallback)
    {
        if (consequence != null)
        {
            consequence(quest);
        }
        else
        {
            fallback();
        }
    }
}
