using GameInterface.Services.Issues.Framework.Finalization;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Ends issues whose owning player is gone or whose ending threw.</summary>
public interface IActiveIssueDismisser
{
    // Server: the one way an issue that must end now ends, keeping the outcome through every retry. Each step runs even
    // when one before it threw: with a closeWith, returning a sent troops issue's troops when their return never ran and
    // sending its owner the closeWith ending when it was sent none (run both inside the owner's scope), the ending (the
    // dismissal for its state when none is given), dropping its owner entry, closing the owner's unfinished row with the
    // ending it was sent or closeWith (dropping it when null), then announcing it once it is out of the manager, or trying
    // again after the next daily tick. No vanilla outcome runs, so nothing is paid. One already waiting for that retry is
    // left to it.
    void End(IssueBase issue, IssueOutcome outcome, Action ending = null, IssueBase.IssueUpdateDetails? closeWith = null,
        bool announce = true);

    // Server: ends a cancel or troop completion that threw as the owner, disposing the owner scope after
    void EndThrown(IssueBase issue, IssueOutcome outcome, IDisposable owner);

    // Server: taken before a sent troops cancel or completion runs vanilla's consequences and troop return. False when
    // one already started, so it must not run again, End finishes it.
    bool TryClaim(IssueBase issue);

    // Server: every troop return notes it on the issue's claim, so End never returns them again
    void NoteTroopReturn(IssueBase issue);

    // Server daily tick: takes every issue whose ending threw out of the manager and ends it the same way
    void RetryFailedDismissals();
}
