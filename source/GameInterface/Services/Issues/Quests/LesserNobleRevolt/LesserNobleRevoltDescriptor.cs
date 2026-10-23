using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal sealed class LesserNobleRevoltDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }
    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public LesserNobleRevoltDescriptor(
        LesserNobleRevoltCreationCaptureStrategy creationCaptureStrategy,
        LesserNobleRevoltQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        LesserNobleRevoltAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy,
        LesserNobleRevoltFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
