using GameInterface.Services.Issues.Framework.Finalization;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Ends issues whose owning player is gone or whose ending threw.</summary>
public interface IActiveIssueDismisser
{
    // Server: ends the issue of a player who is gone without any vanilla outcome and drops its unfinished journal rows.
    // Startup passes announce false, nobody is connected to hear it. Each step runs even when one before it threw.
    void EndWithoutOwner(IssueBase issue, IssueOutcome outcome, bool announce);

    // Server: runs ending inside the owner scope the caller opened, then disposes it. Sent troops whose return or ending
    // never ran come back and the owner's open row ends as a cancel. Each step runs even when one before it threw.
    void EndAsOwner(IssueBase issue, IssueOutcome outcome, Action ending, IDisposable owner);

    // Server: ends a cancel or troop completion that threw as the owner without running vanilla again, see EndAsOwner.
    // Returns null, so the daily tick or the death that ran it goes on instead of picking the issue again.
    Exception EndAfterThrow(IssueBase issue, IssueOutcome outcome, IDisposable owner, Exception exception);

    // Server daily tick: takes every issue whose ending threw out of the manager without running that ending again
    void RetryFailedEndings();
}
