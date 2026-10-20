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

    // The server decides the outcome itself, see CapturedByBountyHuntersOutcomePatches
    public IFinalizationProofStrategy FinalizationProofStrategy => null;

    public CapturedByBountyHuntersDescriptor(
        CapturedByBountyHuntersCreationCaptureStrategy creationCaptureStrategy,
        CapturedByBountyHuntersQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        CapturedByBountyHuntersAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
    }
    public bool SuccessOnlyThroughBranchProof => false;
}
