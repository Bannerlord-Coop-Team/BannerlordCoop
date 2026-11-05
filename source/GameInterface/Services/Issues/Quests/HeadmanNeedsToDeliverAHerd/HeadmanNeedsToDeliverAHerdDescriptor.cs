using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

internal class HeadmanNeedsToDeliverAHerdDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }
    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public HeadmanNeedsToDeliverAHerdDescriptor(
        HeadmanNeedsToDeliverAHerdCreationCaptureStrategy creation,
        HeadmanNeedsToDeliverAHerdQuestSolutionAcceptStrategy accept,
        HeadmanNeedsToDeliverAHerdAlternativeSolutionAcceptStrategy alternative,
        HeadmanNeedsToDeliverAHerdFinalizationProofStrategy finalization)
    {
        CreationCaptureStrategy = creation;
        QuestSolutionAcceptStrategy = accept;
        AlternativeSolutionAcceptStrategy = alternative;
        FinalizationProofStrategy = finalization;
    }
}
