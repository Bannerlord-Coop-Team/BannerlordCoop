using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssue;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal class LadysKnightOutDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy => null;
    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public LadysKnightOutDescriptor(
        LadysKnightOutCreationCaptureStrategy creationCaptureStrategy,
        LadysKnightOutQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        LadysKnightOutFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
