using GameInterface.Services.Issues.Framework.Interface;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
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

        var issue = new Issue(issueOwner);

        // The issue only exists to be read, vanilla's constructor listens to settlement owner changes for it
        CampaignEvents.OnSettlementOwnerChangedEvent.ClearListeners(issue);

        // No market sells the weapon, so the quest would ask for nothing
        if (issue._averagePriceForItem <= 0)
        {
            return false;
        }

        // A town the player owns or one that is too loyal would lose the issue on the next daily tick
        if (!issue.IssueStayAliveConditions())
        {
            return false;
        }

        captured = Capture(issue);
        return true;
    }
}
