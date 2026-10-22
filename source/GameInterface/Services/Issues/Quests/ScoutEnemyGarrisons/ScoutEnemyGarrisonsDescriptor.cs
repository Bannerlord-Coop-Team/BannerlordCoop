using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons;

internal class ScoutEnemyGarrisonsDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy => null;

    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public ScoutEnemyGarrisonsDescriptor(
        ScoutEnemyGarrisonsCreationCaptureStrategy creationCaptureStrategy,
        ScoutEnemyGarrisonsQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        ScoutEnemyGarrisonsFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
