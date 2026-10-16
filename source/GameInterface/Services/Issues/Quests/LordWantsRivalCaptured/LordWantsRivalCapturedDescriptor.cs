using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Interfaces;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordWantsRivalCaptured;

internal sealed class LordWantsRivalCapturedDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy => null;
    public IFinalizationProofStrategy FinalizationProofStrategy => null;

    public LordWantsRivalCapturedDescriptor(
        LordWantsRivalCapturedCreationCaptureStrategy creationCaptureStrategy,
        LordWantsRivalCapturedQuestSolutionAcceptStrategy questSolutionAcceptStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
    }
}
