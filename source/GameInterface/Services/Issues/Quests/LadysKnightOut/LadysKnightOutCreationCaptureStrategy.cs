using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssue;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal class LadysKnightOutCreationCaptureStrategy : ICreationCaptureStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();
    public IssueBase CreateIssue(Hero issueOwner, byte[] captured) => new Issue(issueOwner);
    public void ApplyAfterCreation(IssueBase issue, byte[] captured) { }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = Array.Empty<byte>();
        return issueOwner.IsLord && issueOwner.IsFemale && issueOwner.IsNoncombatant &&
            issueOwner.CurrentSettlement?.IsTown == true && issueOwner.CurrentSettlement.Town.Security <= 80;
    }
}
