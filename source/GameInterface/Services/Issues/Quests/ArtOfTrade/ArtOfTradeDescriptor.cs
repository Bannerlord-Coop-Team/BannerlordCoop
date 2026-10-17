using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue;

namespace GameInterface.Services.Issues.Quests.ArtOfTrade;

internal class ArtOfTradeDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }
    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public ArtOfTradeDescriptor(
        ArtOfTradeCreationCaptureStrategy creationCaptureStrategy,
        ArtOfTradeQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        ArtOfTradeAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy,
        ArtOfTradeFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
