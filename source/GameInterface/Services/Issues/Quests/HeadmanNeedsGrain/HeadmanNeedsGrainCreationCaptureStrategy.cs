using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Behavior = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

internal class HeadmanNeedsGrainCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public HeadmanNeedsGrainCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var grainIssue = (Issue)issue;
        var market = grainIssue.NearbySuitableSettlement;
        var marketId = string.Empty;
        if (market != null)
        {
            objectManager.TryGetIdWithLogging(market, out marketId);
        }

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(marketId ?? string.Empty);
            writer.Write(grainIssue._issueDifficultyMultiplier);
            writer.Write(grainIssue.IssueDueTime.NumTicks);
            writer.Write(Campaign.Current.GetCampaignBehavior<Behavior>()._averageGrainPriceInCalradia);
            return stream.ToArray();
        }
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured) => new Issue(issueOwner);

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
        using (var stream = new MemoryStream(captured))
        using (var reader = new BinaryReader(stream))
        {
            var marketId = reader.ReadString();
            var grainIssue = (Issue)issue;
            if (marketId.Length != 0 && objectManager.TryGetObjectWithLogging<Settlement>(marketId, out var market))
            {
                grainIssue._nearbySuitableSettlementCache = market;
            }

            grainIssue._issueDifficultyMultiplier = reader.ReadSingle();
            grainIssue.IssueDueTime = new CampaignTime(reader.ReadInt64());
            Campaign.Current.GetCampaignBehavior<Behavior>()._averageGrainPriceInCalradia = reader.ReadInt32();
        }
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if (!issueOwner.IsHeadman || issueOwner.CurrentSettlement?.Village?.Bound?.IsTown != true)
        {
            return false;
        }

        captured = Capture(new Issue(issueOwner));
        return true;
    }
}
