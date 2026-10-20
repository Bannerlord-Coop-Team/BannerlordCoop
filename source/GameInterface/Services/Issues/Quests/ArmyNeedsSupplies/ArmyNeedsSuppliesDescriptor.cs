using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;

namespace GameInterface.Services.Issues.Quests.ArmyNeedsSupplies;

internal class ArmyNeedsSuppliesDescriptor : IQuestTypeDescriptor, IBranchOnlyFinalizationDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy => null;

    public IFinalizationProofStrategy FinalizationProofStrategy { get; }
    public bool SuccessOnlyThroughBranchProof => true;

    public ArmyNeedsSuppliesDescriptor(
        ArmyNeedsSuppliesCreationCaptureStrategy creationCaptureStrategy,
        ArmyNeedsSuppliesQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        ArmyNeedsSuppliesFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
