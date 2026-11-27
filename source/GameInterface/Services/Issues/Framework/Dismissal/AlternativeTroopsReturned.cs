using Common.Messaging;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>Published on the server once an issue's sent troops returned, by vanilla or by their quest.</summary>
public readonly struct AlternativeTroopsReturned : IEvent
{
    public readonly IssueBase Issue;

    public AlternativeTroopsReturned(IssueBase issue)
    {
        Issue = issue;
    }
}
