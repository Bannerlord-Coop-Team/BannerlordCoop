using Common.Messaging;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Messages;

/// <summary>
/// Local event published on the server after vanilla picked new targets for an offered issue.
/// </summary>
public readonly struct ScoutEnemyGarrisonsTargetsChanged : IEvent
{
    public readonly IssueBase Issue;

    public ScoutEnemyGarrisonsTargetsChanged(IssueBase issue)
    {
        Issue = issue;
    }
}
