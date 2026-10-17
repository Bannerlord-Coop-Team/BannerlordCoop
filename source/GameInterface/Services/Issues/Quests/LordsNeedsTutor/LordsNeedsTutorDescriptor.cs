using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor;

internal sealed class LordsNeedsTutorDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy => null;
    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public LordsNeedsTutorDescriptor(
        LordsNeedsTutorCreationCaptureStrategy creationCaptureStrategy,
        LordsNeedsTutorQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        LordsNeedsTutorFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
