using Common.Messaging;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.CreationCapture;

/// <summary>
/// Local event published on the server after vanilla created an issue.
/// </summary>
public readonly struct IssueCreated : IEvent
{
    public readonly IssueBase Issue;

    public IssueCreated(IssueBase issue)
    {
        Issue = issue;
    }
}
