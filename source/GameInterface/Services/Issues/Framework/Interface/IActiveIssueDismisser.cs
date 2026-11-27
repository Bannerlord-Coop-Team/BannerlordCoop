using GameInterface.Services.Issues.Framework.Finalization;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Ends issues whose owning player is gone or whose ending threw.</summary>
public interface IActiveIssueDismisser
{
    // Server: ends an issue now without any vanilla outcome, each step isolated, see Issues/AGENTS.md
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
