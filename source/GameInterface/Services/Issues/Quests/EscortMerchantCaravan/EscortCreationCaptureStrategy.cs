using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

internal sealed class EscortCreationCaptureStrategy : ICreationCaptureStrategy
{
    public byte[] Capture(IssueBase issue) => BitConverter.GetBytes(((Issue)issue)._companionRewardRandom);

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        var issue = new Issue(issueOwner);
        ApplyAfterCreation(issue, captured);
        return issue;
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
        ((Issue)issue)._companionRewardRandom = BitConverter.ToInt32(captured, 0);
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if (!issueOwner.IsMerchant || issueOwner.CurrentSettlement?.IsTown != true)
            return false;

        captured = Capture(new Issue(issueOwner));
        return true;
    }
}
