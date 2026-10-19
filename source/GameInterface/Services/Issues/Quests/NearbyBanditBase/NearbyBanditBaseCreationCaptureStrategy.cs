using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase;

internal class NearbyBanditBaseCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public NearbyBanditBaseCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        return Write(((Issue)issue).TargetHideout);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        if (!objectManager.TryGetObjectWithLogging<Settlement>(Encoding.UTF8.GetString(captured), out var hideout))
        {
            return null;
        }

        return new Issue(issueOwner, hideout);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        var village = issueOwner.CurrentSettlement;
        if (!issueOwner.IsHeadman || village == null || !village.IsVillage)
        {
            return false;
        }

        var hideout = Campaign.Current.AllHideouts
            .Where(candidate => candidate.IsInfested)
            .OrderBy(candidate => candidate.Settlement.Position.ToVec2().DistanceSquared(village.Position.ToVec2()))
            .FirstOrDefault();
        if (hideout == null)
        {
            return false;
        }

        captured = Write(hideout.Settlement);
        return true;
    }

    private byte[] Write(Settlement hideout)
    {
        objectManager.TryGetIdWithLogging(hideout, out var hideoutId);

        return Encoding.UTF8.GetBytes(hideoutId ?? string.Empty);
    }
}
