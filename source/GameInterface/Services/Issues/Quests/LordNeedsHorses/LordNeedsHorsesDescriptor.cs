using GameInterface.Services.Issues.Framework.Interface;
using System;
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;

namespace GameInterface.Services.Issues.Quests.LordNeedsHorses;

internal class LordNeedsHorsesDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);

    public ICreationCaptureStrategy CreationCaptureStrategy { get; }

    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }

    // The hand-in has no branches to tell apart, the server checks the mounts in LordNeedsHorsesHandInPatch
    public IFinalizationProofStrategy FinalizationProofStrategy => null;

    public LordNeedsHorsesDescriptor(
        LordNeedsHorsesCreationCaptureStrategy creationCaptureStrategy,
        LordNeedsHorsesQuestSolutionAcceptStrategy questSolutionAcceptStrategy,
        LordNeedsHorsesAlternativeSolutionAcceptStrategy alternativeSolutionAcceptStrategy)
    {
        CreationCaptureStrategy = creationCaptureStrategy;
        QuestSolutionAcceptStrategy = questSolutionAcceptStrategy;
        AlternativeSolutionAcceptStrategy = alternativeSolutionAcceptStrategy;
    }
}
