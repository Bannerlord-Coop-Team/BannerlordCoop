using GameInterface.Services.Issues.Framework.Finalization;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Server: ends an issue without its vanilla outcome once, and announces it once it is out of the manager.</summary>
public interface IIssueEndingCoordinator
{
    // Ends the issue of a player who is gone without any vanilla outcome and drops its unfinished journal rows.
    // Startup passes announce false, nobody is connected to hear it. Each step runs even when one before it threw.
    void EndWithoutOwner(IssueBase issue, IssueOutcome outcome, bool announce);

    // Runs ending inside the owner scope the caller opened, then disposes it. Sent troops whose return or ending
    // never ran come back and the owner's open row ends as a cancel. Each step runs even when one before it threw.
    void EndAsOwner(IssueBase issue, IssueOutcome outcome, Action ending, IDisposable owner);

    // Ends a cancel or troop completion that threw as the owner without running vanilla again, see EndAsOwner. Returns
    // null, so the daily tick or the death that ran it goes on. An issue still in the manager waits for the daily retry.
    Exception EndAfterThrow(IssueBase issue, IssueOutcome outcome, IDisposable owner, Exception exception);

    // Daily tick: tries again to take every issue whose ending threw out of the manager, deactivating it if that throws
    void RetryFailedEndings();
}
