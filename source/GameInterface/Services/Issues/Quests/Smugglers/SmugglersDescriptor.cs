using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssue;

namespace GameInterface.Services.Issues.Quests.Smugglers;

internal class SmugglersDescriptor : IQuestTypeDescriptor, IBranchOnlyFinalizationDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }

    public IFinalizationProofStrategy FinalizationProofStrategy { get; }
    public bool SuccessOnlyThroughBranchProof => true;

    public SmugglersDescriptor(
        SmugglersCreationCaptureStrategy creationCaptureStrategy,
        SmugglersQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        SmugglersAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy,
        SmugglersFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
