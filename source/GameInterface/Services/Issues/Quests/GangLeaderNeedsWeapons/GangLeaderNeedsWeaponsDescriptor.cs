using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons;

internal class GangLeaderNeedsWeaponsDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }

    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public GangLeaderNeedsWeaponsDescriptor(
        GangLeaderNeedsWeaponsCreationCaptureStrategy creationCaptureStrategy,
        GangLeaderNeedsWeaponsQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        GangLeaderNeedsWeaponsAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy,
        GangLeaderNeedsWeaponsFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
