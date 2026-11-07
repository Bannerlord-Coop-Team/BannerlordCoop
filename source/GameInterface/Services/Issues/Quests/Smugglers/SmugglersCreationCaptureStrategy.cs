using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssue;

namespace GameInterface.Services.Issues.Quests.Smugglers;

internal class SmugglersCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public SmugglersCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var smugglersIssue = (Issue)issue;

        return Write(IdOf(smugglersIssue._targetSettlement), IdOf(smugglersIssue._originSettlement));
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        Read(captured, out var targetSettlementId, out var originSettlementId);

        objectManager.TryGetObjectWithLogging<Settlement>(targetSettlementId, out var targetSettlement);
        objectManager.TryGetObjectWithLogging<Settlement>(originSettlementId, out var originSettlement);

        return new Issue(issueOwner, new KeyValuePair<Settlement, Settlement>(targetSettlement, originSettlement));
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        if (!issueOwner.IsLord || issueOwner.Clan == null)
        {
            return false;
        }

        // A town the lord owns and its nearest other town, like vanilla but without its random pick and player checks
        var targetSettlement = issueOwner.Clan.Settlements.FirstOrDefault(settlement => settlement.IsTown && settlement.Owner == issueOwner);
        if (targetSettlement == null)
        {
            return false;
        }

        var originTown = SettlementHelper.FindNearestTownToSettlement(targetSettlement, MobileParty.NavigationType.Default, town => town != targetSettlement);
        if (originTown == null)
        {
            return false;
        }

        captured = Write(IdOf(targetSettlement), IdOf(originTown.Settlement));
        return true;
    }

    private string IdOf(object obj)
    {
        if (obj == null)
        {
            return string.Empty;
        }

        objectManager.TryGetIdWithLogging(obj, out var id);
        return id ?? string.Empty;
    }

    private static byte[] Write(string targetSettlementId, string originSettlementId)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(targetSettlementId);
                writer.Write(originSettlementId);
                writer.Flush();

                return stream.ToArray();
            }
        }
    }

    private static void Read(byte[] captured, out string targetSettlementId, out string originSettlementId)
    {
        using (var stream = new MemoryStream(captured))
        {
            using (var reader = new BinaryReader(stream))
            {
                targetSettlementId = reader.ReadString();
                originSettlementId = reader.ReadString();
            }
        }
    }
}
