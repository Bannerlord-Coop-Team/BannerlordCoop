using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.OwnerChange;

/// <summary>
/// Local event published on the server after vanilla moved an issue to another hero.
/// </summary>
public readonly struct IssueOwnerChanged : IEvent
{
    public readonly IssueBase Issue;
    public readonly Hero OldOwner;

    public IssueOwnerChanged(IssueBase issue, Hero oldOwner)
    {
        Issue = issue;
        OldOwner = oldOwner;
    }
}
