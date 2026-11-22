using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

/// <summary>Enables the deserter quest through the shared ownership framework.</summary>
internal class ExtortionByDesertersDescriptor : IQuestTypeDescriptor
{
    public Type IssueType => typeof(Issue);
    public ICreationCaptureStrategy CreationCaptureStrategy { get; }
    public IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }
    public IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }
    public IFinalizationProofStrategy FinalizationProofStrategy { get; }

    public ExtortionByDesertersDescriptor(
        ExtortionByDesertersCreationCaptureStrategy creation,
        ExtortionByDesertersQuestSolutionAcceptStrategy accept,
        ExtortionByDesertersAlternativeSolutionAcceptStrategy alternative,
        ExtortionByDesertersFinalizationProofStrategy finalization)
    {
        CreationCaptureStrategy = creation;
        QuestSolutionAcceptStrategy = accept;
        AlternativeSolutionAcceptStrategy = alternative;
        FinalizationProofStrategy = finalization;
    }
}
