using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue;

namespace GameInterface.Services.Issues.Quests.LordNeedsGarrisonTroops;

internal class LordNeedsGarrisonTroopsDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }
    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public LordNeedsGarrisonTroopsDescriptor(
        LordNeedsGarrisonTroopsCreationCaptureStrategy creationCaptureStrategy,
        LordNeedsGarrisonTroopsQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        LordNeedsGarrisonTroopsAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy,
        LordNeedsGarrisonTroopsFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
