using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>Runs cancellation as the owner and retains blocked cancellations for this session.</summary>
public interface IIssueCancellationCoordinator
{
    bool IsPending(IssueBase issue);
    bool BeforeCancellation(IssueBase issue, TextObject log, out IDisposable owner);
    bool BeforeGiverDeath(Hero victim, out IDisposable owner);
    bool BeforeGiverOwnerChange(IssueBase issue, Hero deadOwner);
    void CancelForPlayerDeath(string controllerId, Hero oldHero, MobileParty originalParty);

    // Server: ends a cancellation that threw while running as the owner, disposing the owner scope. End returns the
    // troops if their return never started, sends the owner the cancel if it was sent none, finalizes, and announces
    // once. Returns null so the caller goes on.
    Exception AfterCancellationException(IssueBase issue, IDisposable owner, Exception exception);

    void Retry();
}
