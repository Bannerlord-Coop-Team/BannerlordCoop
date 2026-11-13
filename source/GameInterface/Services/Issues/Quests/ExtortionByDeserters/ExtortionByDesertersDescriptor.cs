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

/// <summary>The issue constructor has no random quest-specific fields.</summary>
internal class ExtortionByDesertersCreationCaptureStrategy : ICreationCaptureStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();
    public IssueBase CreateIssue(Hero owner, byte[] captured) => new Issue(owner);
    public void ApplyAfterCreation(IssueBase issue, byte[] captured) { }

    public bool TryBuildDebugCapture(Hero owner, out byte[] captured)
    {
        captured = Array.Empty<byte>();
        return owner.IsHeadman && owner.CurrentSettlement?.IsVillage == true &&
            owner.CurrentSettlement.Village.Bound?.Town != null;
    }
}

/// <summary>The framework captures companion risk, casualties and return times.</summary>
internal class ExtortionByDesertersAlternativeSolutionAcceptStrategy : IAlternativeSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();
    public void Apply(IssueBase issue, byte[] captured) { }
}
