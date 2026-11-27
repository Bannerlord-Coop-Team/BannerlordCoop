using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

internal class HeadmanNeedsToDeliverAHerdDescriptor : IQuestTypeDescriptor, ITroopReturnDescriptor
{
    private readonly IHeadmanHerdCleanup cleanup;

    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }
    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public HeadmanNeedsToDeliverAHerdDescriptor(
        HeadmanNeedsToDeliverAHerdCreationCaptureStrategy creation,
        HeadmanNeedsToDeliverAHerdQuestSolutionAcceptStrategy accept,
        HeadmanNeedsToDeliverAHerdAlternativeSolutionAcceptStrategy alternative,
        HeadmanNeedsToDeliverAHerdFinalizationProofStrategy finalization,
        IHeadmanHerdCleanup cleanup)
    {
        CreationCaptureStrategy = creation;
        QuestSolutionAcceptStrategy = accept;
        AlternativeSolutionAcceptStrategy = alternative;
        FinalizationProofStrategy = finalization;
        this.cleanup = cleanup;
    }

    // The troops go to the owner's party the completion or cancellation substituted in, not vanilla's main party
    public bool ReturnTroops(IssueBase issue) => cleanup.ReturnAlternativeTroops(issue);
}
