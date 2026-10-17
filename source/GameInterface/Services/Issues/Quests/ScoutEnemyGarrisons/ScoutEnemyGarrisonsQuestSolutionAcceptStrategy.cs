using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;
using QuestSettlement = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.QuestSettlement;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons;

/// <summary>
/// Every machine rerolls an offered issue's targets on its own once they stop being enemies, so the targets
/// the accepting player was given are forced onto the server's quest object and the other clients' issue.
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
