using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;
using QuestSettlement = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.QuestSettlement;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons;

/// <summary>
/// The server's accept check runs vanilla's stay-alive conditions, which can reroll the targets after the accepting
/// player was given them, so the accepter's targets are forced onto the server's quest object and the other clients' issue.
/// </summary>
internal class ScoutEnemyGarrisonsQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly ScoutEnemyGarrisonsCreationCaptureStrategy targets;

    public ScoutEnemyGarrisonsQuestSolutionAcceptStrategy(ScoutEnemyGarrisonsCreationCaptureStrategy targets)
    {
        this.targets = targets;
    }

    public byte[] Capture(IssueBase issue) => targets.Capture(issue);

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue is Issue scoutIssue) || !targets.TryReadTargets(captured, out var settlements))
        {
            return;
        }

        scoutIssue._settlement1 = settlements[0];
        scoutIssue._settlement2 = settlements[1];
        scoutIssue._settlement3 = settlements[2];

        if (!(issue.IssueQuest is Quest quest))
        {
            return;
        }

        quest._questSettlement1 = new QuestSettlement(settlements[0], 0);
        quest._questSettlement2 = new QuestSettlement(settlements[1], 0);
        quest._questSettlement3 = new QuestSettlement(settlements[2], 0);
    }
}
