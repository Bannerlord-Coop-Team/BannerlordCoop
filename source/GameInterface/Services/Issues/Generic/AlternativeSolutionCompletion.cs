using System;
using Common;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic;

public sealed class AlternativeSolutionCompletionAuthorityGuard : IDisposable
{
    [ThreadStatic]
    private static int _count;

    public AlternativeSolutionCompletionAuthorityGuard() => _count++;

    public void Dispose() => _count = _count > 0 ? _count - 1 : 0;

    public static bool IsActive => _count > 0;
}

public static class AlternativeSolutionCompletionRunner
{
    public static bool TryTriggerOwnedCompletion(Hero owner, Action<Hero> requestServerCompletion)
    {
        if (owner?.Issue is not IssueBase issue) return false;
        if (!issue.IsSolvingWithAlternative || !issue.AlternativeSolutionReturnTimeForTroops.IsPast) return false;
        if (!IssueOwnershipRegistry.IsLocalPeerOwner(owner)) return false;

        if (ModInformation.IsServer)
        {
            CompleteOnServer(owner, issue);
        }
        else
        {
            requestServerCompletion(owner);
        }

        return true;
    }

    public static void CompleteOnServer(Hero owner, IssueBase issue)
    {
        IssueManagerQuestCompletedReasonCapture.PendingReasons[owner] = IssueFinalizeReason.AlternativeSolutionSuccess;

        using (new AlternativeSolutionCompletionAuthorityGuard())
        {
            issue.CompleteIssueWithAlternativeSolution();
        }
    }
}
