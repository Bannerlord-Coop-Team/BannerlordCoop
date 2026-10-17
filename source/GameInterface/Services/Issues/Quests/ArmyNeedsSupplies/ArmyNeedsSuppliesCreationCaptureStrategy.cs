using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;

namespace GameInterface.Services.Issues.Quests.ArmyNeedsSupplies;

internal class ArmyNeedsSuppliesCreationCaptureStrategy : ICreationCaptureStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured) => new Issue(issueOwner);

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        if (issueOwner.PartyBelongedTo?.Army?.ArmyOwner != issueOwner)
        {
            return false;
        }

        captured = Array.Empty<byte>();
        return true;
    }
}
