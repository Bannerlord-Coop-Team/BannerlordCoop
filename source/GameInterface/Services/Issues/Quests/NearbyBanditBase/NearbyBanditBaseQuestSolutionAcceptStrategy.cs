using Common;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase;

internal class NearbyBanditBaseQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
        // NearbyBanditBaseQuestAcceptedPatch leaves spotting out on the client, AutoSync sends this write there
        if (ModInformation.IsServer && issue is Issue banditBaseIssue)
        {
            banditBaseIssue.TargetHideout.Hideout.IsSpotted = true;
        }
    }
}
