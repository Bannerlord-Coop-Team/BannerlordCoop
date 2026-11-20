using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;

internal class LandLordCompanyOfTroubleCreationCaptureStrategy : ICreationCaptureStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured) => new Issue(issueOwner);

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        if (!issueOwner.IsLord || issueOwner.PartyBelongedTo == null)
        {
            return false;
        }

        captured = Array.Empty<byte>();
        return true;
    }
}
