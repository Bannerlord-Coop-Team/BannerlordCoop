using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssue;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal class CaravanAmbushCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public CaravanAmbushCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        if (!objectManager.TryGetIdWithLogging(((Issue)issue)._targetSettlement, out var targetId))
        {
            throw new InvalidOperationException("The caravan destination has no network identity");
        }
        return Encoding.UTF8.GetBytes(targetId);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        if (!objectManager.TryGetObjectWithLogging<Settlement>(Encoding.UTF8.GetString(captured), out var target))
        {
            throw new InvalidOperationException("The caravan destination is missing");
        }
        return new Issue(issueOwner, target);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured) { }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        var behavior = Campaign.Current.GetCampaignBehavior<CaravanAmbushIssueBehavior>();
        if (!behavior.ConditionsHold(issueOwner)) return false;
        var target = behavior.GetTargetSettlement(issueOwner.CurrentSettlement);
        if (target == null || !objectManager.TryGetIdWithLogging(target, out var id)) return false;
        captured = Encoding.UTF8.GetBytes(id);
        return true;
    }
}
