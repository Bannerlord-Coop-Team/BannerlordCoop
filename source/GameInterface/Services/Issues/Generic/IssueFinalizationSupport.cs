using Common.Util;
using GameInterface.Services.Issues.Messages;
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
    public static void FinalizeMirror(Hero owner, IssueFinalizeReason reason)
    {
        if (owner?.Issue == null) return;

        using (new IssueFinalizeAuthorityGuard())
        using (new AllowedThread())
        {
            var quest = owner.Issue.IssueQuest;
            if (quest != null && quest.IsOngoing)
            {
                switch (reason)
                {
                    case IssueFinalizeReason.QuestSuccess:
                        quest.CompleteQuestWithSuccess();
                        return;
                    case IssueFinalizeReason.QuestCancel:
                        quest.CompleteQuestWithCancel();
                        return;
                    case IssueFinalizeReason.QuestFail:
                        quest.CompleteQuestWithFail();
                        return;
                    case IssueFinalizeReason.QuestTimeout:
                        quest.CompleteQuestWithTimeOut();
                        return;
                    case IssueFinalizeReason.QuestBetrayal:
                        quest.CompleteQuestWithBetrayal();
                        return;
                    default:
                        quest.CompleteQuestWithCancel();
                        return;
                }
            }

            if (reason == IssueFinalizeReason.RejectedAccept)
            {
                owner.Issue.CompleteIssueWithCancel();
                return;
            }

            owner.Issue.IssueFinalized();
        }
    }
}
