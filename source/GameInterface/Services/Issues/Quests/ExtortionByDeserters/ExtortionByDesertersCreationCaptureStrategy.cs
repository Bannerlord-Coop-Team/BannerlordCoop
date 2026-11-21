using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Services.Issues.Framework.Interface;
using Issue = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

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
