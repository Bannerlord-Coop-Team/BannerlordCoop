using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters;

internal class CapturedByBountyHuntersDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }

    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public CapturedByBountyHuntersDescriptor(
        CapturedByBountyHuntersCreationCaptureStrategy creationCaptureStrategy,
        CapturedByBountyHuntersQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        CapturedByBountyHuntersAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy,
        CapturedByBountyHuntersFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
