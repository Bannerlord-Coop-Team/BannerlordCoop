using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

internal class HeadmanNeedsToDeliverAHerdCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public HeadmanNeedsToDeliverAHerdCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var herd = (Issue)issue;
        if (!objectManager.TryGetIdWithLogging(herd._targetSettlement, out var settlementId) ||
            !objectManager.TryGetIdWithLogging(herd._targetHero, out var heroId) ||
            !objectManager.TryGetIdWithLogging(herd._herdTypeToDeliver, out var itemId))
        {
            throw new InvalidOperationException("The herd's destination, recipient and livestock must be registered");
        }

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(settlementId);
            writer.Write(heroId);
            writer.Write(itemId);
            writer.Flush();
            return stream.ToArray();
        }
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        using (var stream = new MemoryStream(captured))
        using (var reader = new BinaryReader(stream))
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(reader.ReadString(), out var settlement) ||
                !objectManager.TryGetObjectWithLogging<Hero>(reader.ReadString(), out var hero) ||
                !objectManager.TryGetObjectWithLogging<ItemObject>(reader.ReadString(), out var item))
            {
                throw new InvalidOperationException("The herd's captured destination, recipient or livestock could not be resolved");
            }

            var issue = new Issue(issueOwner);
            issue._targetSettlement = settlement;
            issue._targetHero = hero;
            issue._herdTypeToDeliver = item;
            return issue;
        }
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if ((!issueOwner.IsHeadman && !issueOwner.IsRuralNotable) ||
            issueOwner.CurrentSettlement?.IsVillage != true ||
            issueOwner.CurrentSettlement.Village.Bound == null ||
            issueOwner.CurrentSettlement.Village.Bound.Notables.Count == 0)
        {
            return false;
        }

        captured = Capture(new Issue(issueOwner));
        return true;
    }
}
