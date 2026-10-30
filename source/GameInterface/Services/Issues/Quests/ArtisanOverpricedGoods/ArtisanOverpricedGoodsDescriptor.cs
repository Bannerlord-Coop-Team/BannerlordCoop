using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;

internal class ArtisanOverpricedGoodsDescriptor : IQuestTypeDescriptor, ISpecializedEndingJournalDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }
    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public ArtisanOverpricedGoodsDescriptor(
        ArtisanOverpricedGoodsCreationCaptureStrategy creation,
        ArtisanOverpricedGoodsQuestSolutionAcceptStrategy accept,
        ArtisanOverpricedGoodsAlternativeSolutionAcceptStrategy alternative,
        ArtisanOverpricedGoodsFinalizationProofStrategy finalization)
    {
        CreationCaptureStrategy = creation;
        QuestSolutionAcceptStrategy = accept;
        AlternativeSolutionAcceptStrategy = alternative;
        FinalizationProofStrategy = finalization;
    }
}
