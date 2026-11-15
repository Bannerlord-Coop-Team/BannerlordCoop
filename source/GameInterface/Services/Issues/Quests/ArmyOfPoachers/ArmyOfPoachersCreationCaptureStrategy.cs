using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.IO;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers;

internal class ArmyOfPoachersCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public ArmyOfPoachersCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        objectManager.TryGetIdWithLogging(((Issue)issue)._questVillage.Settlement, out var id);
        return Encoding.UTF8.GetBytes(id);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        if (!objectManager.TryGetObjectWithLogging<Settlement>(Encoding.UTF8.GetString(captured), out var settlement) || settlement.Village == null)
        {
            throw new InvalidDataException("The poachers issue needs its server-selected village");
        }

        return new Issue(issueOwner, settlement.Village);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if (!issueOwner.IsMerchant || issueOwner.CurrentSettlement?.Town == null)
        {
            return false;
        }

        var village = issueOwner.CurrentSettlement.BoundVillages.FirstOrDefault(candidate =>
            !candidate.Settlement.IsUnderRaid && !candidate.Settlement.IsRaided);
        if (village == null || !objectManager.TryGetIdWithLogging(village.Settlement, out var id))
        {
            return false;
        }

        captured = Encoding.UTF8.GetBytes(id);
        return true;
    }
}
