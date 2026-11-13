using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.RevenueFarmingIssueBehavior.RevenueFarmingIssue;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

internal sealed class RevenueFarmingDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy => null;

    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public RevenueFarmingDescriptor(RevenueFarmingCreationCaptureStrategy creation,
        RevenueFarmingQuestSolutionAcceptStrategy accept, RevenueFarmingFinalizationProofStrategy proof)
    {
        CreationCaptureStrategy = creation;
        QuestSolutionAcceptStrategy = accept;
        FinalizationProofStrategy = proof;
    }
}
