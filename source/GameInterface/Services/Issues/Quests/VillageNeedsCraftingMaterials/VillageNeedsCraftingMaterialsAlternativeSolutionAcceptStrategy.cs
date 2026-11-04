using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue;

namespace GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials;

// The server promises the payment from its own market price when the alternative solution starts
internal class VillageNeedsCraftingMaterialsAlternativeSolutionAcceptStrategy : IAlternativeSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue)
    {
        return BitConverter.GetBytes(((Issue)issue)._promisedPayment);
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (captured.Length < sizeof(int))
        {
            return;
        }

        ((Issue)issue)._promisedPayment = BitConverter.ToInt32(captured, 0);
    }
}
