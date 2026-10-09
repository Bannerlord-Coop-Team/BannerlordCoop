using Common.Messaging;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// Local event published on a client after its player accepted an issue's quest solution.
/// </summary>
public readonly struct QuestSolutionAcceptTriggered : IEvent
{
    public readonly IssueBase Issue;

    public QuestSolutionAcceptTriggered(IssueBase issue)
    {
        Issue = issue;
    }
}
