using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
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
    private readonly IIssueOwnerResolver ownerResolver;

    public ScoutEnemyGarrisonsQuestSolutionAcceptStrategy(
        ScoutEnemyGarrisonsCreationCaptureStrategy targets, IIssueOwnerResolver ownerResolver)
    {
        this.targets = targets;
        this.ownerResolver = ownerResolver;
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

        if (ModInformation.IsServer && issue.IsSolvingWithQuest && quest._startQuestLog == null &&
            ownerResolver.TryResolveOwnerHero(issue, out var owner))
        {
            var mirrored = quest.JournalEntries.ToArray().FirstOrDefault(log =>
                log.LogText?.ToString() == quest.PlayerStartsQuestLogText?.ToString());
            if (mirrored != null)
            {
                quest._startQuestLog = mirrored;
                return;
            }

            // World-event listeners read this progress log without starting the server's quest.
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                quest._startQuestLog = quest.AddDiscreteLog(quest.PlayerStartsQuestLogText,
                    new TextObject("{=jpBpwgAs}Settlements"), quest._scoutedSettlementCount, 3);
        }
    }
}
