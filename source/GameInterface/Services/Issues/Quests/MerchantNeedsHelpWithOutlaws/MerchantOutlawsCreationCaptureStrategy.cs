using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

internal class MerchantOutlawsCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public MerchantOutlawsCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var merchantIssue = (Issue)issue;
        if (!objectManager.TryGetIdWithLogging(merchantIssue.RelatedHideout.Settlement, out var hideoutId))
        {
            throw new InvalidOperationException("The brigands issue has no registered hideout");
        }

        return Encoding.UTF8.GetBytes(hideoutId);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        var hideoutId = Encoding.UTF8.GetString(captured);
        if (!objectManager.TryGetObjectWithLogging<Settlement>(hideoutId, out var settlement) || settlement.Hideout == null)
        {
            throw new InvalidDataException("The brigands issue hideout is missing");
        }

        return new Issue(issueOwner, settlement.Hideout);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if ((!issueOwner.IsMerchant && !issueOwner.IsRuralNotable) || issueOwner.CurrentSettlement == null)
        {
            return false;
        }

        var behavior = Campaign.Current.GetCampaignBehavior<MerchantNeedsHelpWithOutlawsIssueQuestBehavior>();
        if (behavior == null || !behavior.ConditionsHold(issueOwner, out var hideout) ||
            !objectManager.TryGetIdWithLogging(hideout.Settlement, out var hideoutId))
        {
            return false;
        }

        captured = Encoding.UTF8.GetBytes(hideoutId);
        return true;
    }
}
