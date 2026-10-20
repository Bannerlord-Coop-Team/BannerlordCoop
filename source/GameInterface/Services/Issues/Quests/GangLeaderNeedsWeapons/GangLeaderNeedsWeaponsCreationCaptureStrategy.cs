using GameInterface.Services.Issues.Framework.Interface;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons;

/// <summary>
/// The constructor averages the price of the weapon class over the markets it can see and picks the class at
/// random, so the server's pick is forced onto every machine's copy of the issue.
/// </summary>
internal class GangLeaderNeedsWeaponsCreationCaptureStrategy : ICreationCaptureStrategy
{
    public byte[] Capture(IssueBase issue)
    {
        var weaponsIssue = (Issue)issue;

        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(weaponsIssue._requiredWeaponClassIndex);
                writer.Write(weaponsIssue._averagePriceForItem);
                writer.Flush();

                return stream.ToArray();
            }
        }
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        var issue = new Issue(issueOwner);

        using (var stream = new MemoryStream(captured))
        {
            using (var reader = new BinaryReader(stream))
            {
                issue._requiredWeaponClassIndex = reader.ReadInt32();
                issue._averagePriceForItem = reader.ReadInt32();
            }
        }

        return issue;
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        if (issueOwner.CurrentSettlement == null || !issueOwner.CurrentSettlement.IsTown)
        {
            return false;
        }

        var settlement = issueOwner.CurrentSettlement;
        if (settlement.Town == null || settlement.OwnerClan == Clan.PlayerClan || settlement.Town.Loyalty >= 75f)
        {
            return false;
        }

        var averagePrice = CalculateAveragePriceForWeaponClass(WeaponClass.OneHandedAxe);
        // No market sells the weapon, so the quest would ask for nothing
        if (averagePrice <= 0)
        {
            return false;
        }

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(0);
            writer.Write(averagePrice);
            writer.Flush();
            captured = stream.ToArray();
        }

        return true;
    }

    private static int CalculateAveragePriceForWeaponClass(WeaponClass weaponClass)
    {
        var totalPrice = 0;
        var totalAmount = 0;

        foreach (var settlement in Settlement.All)
        {
            if (!settlement.IsTown)
            {
                continue;
            }

            for (var i = 0; i < settlement.ItemRoster.Count; i++)
            {
                var item = settlement.ItemRoster[i];
                var weaponComponent = item.EquipmentElement.Item.WeaponComponent;
                if (weaponComponent != null && weaponComponent.PrimaryWeapon.WeaponClass == weaponClass)
                {
                    totalAmount += item.Amount;
                    totalPrice += item.EquipmentElement.ItemValue;
                }
            }
        }

        return totalPrice / (totalAmount == 0 ? 1 : totalAmount);
    }
}
