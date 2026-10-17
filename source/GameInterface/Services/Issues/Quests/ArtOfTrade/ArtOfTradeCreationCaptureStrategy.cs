using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using Issue = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue;

namespace GameInterface.Services.Issues.Quests.ArtOfTrade;

internal class ArtOfTradeCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public ArtOfTradeCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        return CaptureItem(((Issue)issue)._selectedItemObject, issue.IssueCreationTime);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        using (var reader = new BinaryReader(new MemoryStream(captured)))
        {
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(reader.ReadString(), out var item))
            {
                throw new InvalidDataException("The Art of the Trade production item was not found");
            }

            return new Issue(issueOwner, item) { IssueCreationTime = new CampaignTime(reader.ReadInt64()) };
        }
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if (!issueOwner.IsRuralNotable || issueOwner.CurrentSettlement?.IsVillage != true)
        {
            return false;
        }

        var item = issueOwner.CurrentSettlement.Village.VillageType.PrimaryProduction;
        if (item == null || !objectManager.TryGetIdWithLogging(item, out _))
        {
            return false;
        }

        captured = CaptureItem(item, CampaignTime.Now);
        return true;
    }

    private byte[] CaptureItem(ItemObject item, CampaignTime creationTime)
    {
        if (!objectManager.TryGetIdWithLogging(item, out var itemId))
        {
            throw new InvalidDataException("The Art of the Trade production item has no identity");
        }

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(itemId);
            writer.Write(creationTime.NumTicks);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
