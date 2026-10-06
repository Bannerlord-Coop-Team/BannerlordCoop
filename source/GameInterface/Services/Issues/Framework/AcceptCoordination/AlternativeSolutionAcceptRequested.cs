using Common.Messaging;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// Local event published on a client whose player confirmed the troops for an alternative solution.
/// </summary>
public readonly struct AlternativeSolutionAcceptRequested : IEvent
{
    public readonly IssueBase Issue;

    public AlternativeSolutionAcceptRequested(IssueBase issue)
    {
        Issue = issue;
    }
}
