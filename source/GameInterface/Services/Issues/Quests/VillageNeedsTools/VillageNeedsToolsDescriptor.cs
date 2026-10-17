using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;

namespace GameInterface.Services.Issues.Quests.VillageNeedsTools;

/// <summary>Registers the village tools quest with the shared issue lifecycle.</summary>
internal class VillageNeedsToolsDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }
    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public VillageNeedsToolsDescriptor(
        VillageNeedsToolsCreationCaptureStrategy creationCaptureStrategy,
        VillageNeedsToolsAcceptStrategy acceptStrategy,
        VillageNeedsToolsFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = acceptStrategy;
        AlternativeSolutionAcceptStrategy = acceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
