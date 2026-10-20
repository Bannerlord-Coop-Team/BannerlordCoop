using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;

internal sealed class TheConquestOfSettlementDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy => null;
    public IFinalizationProofStrategy FinalizationProofStrategy => null;

    public TheConquestOfSettlementDescriptor(
        TheConquestOfSettlementCreationCaptureStrategy creationCaptureStrategy,
        TheConquestOfSettlementQuestSolutionAcceptStrategy questSolutionAcceptStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
    }
    public bool SuccessOnlyThroughBranchProof => false;
}
