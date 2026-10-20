using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;

namespace GameInterface.Services.Issues.Quests.LordWantsRivalCaptured;

internal class LordWantsRivalCapturedDescriptor : IQuestTypeDescriptor, IBranchOnlyFinalizationDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy => null;

    public IFinalizationProofStrategy FinalizationProofStrategy { get; }
    public bool SuccessOnlyThroughBranchProof => true;

    public LordWantsRivalCapturedDescriptor(
        LordWantsRivalCapturedCreationCaptureStrategy creationCaptureStrategy,
        LordWantsRivalCapturedQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        LordWantsRivalCapturedFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
