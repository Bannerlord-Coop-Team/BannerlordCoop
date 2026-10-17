using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers;

internal class ArmyOfPoachersDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }

    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public ArmyOfPoachersDescriptor(
        ArmyOfPoachersCreationCaptureStrategy creationCaptureStrategy,
        ArmyOfPoachersQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        ArmyOfPoachersAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy,
        ArmyOfPoachersFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
