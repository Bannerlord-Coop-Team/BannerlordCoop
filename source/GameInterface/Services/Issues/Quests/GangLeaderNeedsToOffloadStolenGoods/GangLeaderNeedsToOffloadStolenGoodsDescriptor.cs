using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

internal class GangLeaderNeedsToOffloadStolenGoodsDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }

    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public GangLeaderNeedsToOffloadStolenGoodsDescriptor(
        GangLeaderNeedsToOffloadStolenGoodsCreationCaptureStrategy creationCaptureStrategy,
        GangLeaderNeedsToOffloadStolenGoodsQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy,
        GangLeaderNeedsToOffloadStolenGoodsFinalizationProofStrategy finalizationProofStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
        FinalizationProofStrategy = finalizationProofStrategy;
    }
}
