using Common.Messaging;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>The issue still owns its quest and final journal when this synchronous event is published.</summary>
public readonly struct IssueJournalObserved : IEvent
{
    public readonly IssueBase Issue;

    public IssueJournalObserved(IssueBase issue) => Issue = issue;
}
