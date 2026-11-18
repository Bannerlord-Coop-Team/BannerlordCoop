using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.RevenueFarmingIssueBehavior.RevenueFarmingIssue;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

internal sealed class RevenueFarmingCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objects;

    public RevenueFarmingCreationCaptureStrategy(IObjectManager objects) => this.objects = objects;

    public byte[] Capture(IssueBase issue) => Write(((Issue)issue)._targetSettlement);

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        var id = Read(captured);
        if (!objects.TryGetObjectWithLogging<Settlement>(id, out var settlement))
            throw new InvalidDataException("Revenue Farming target town is not registered.");
        return new Issue(issueOwner, settlement);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured) { }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if (!issueOwner.IsLord || issueOwner.Clan?.Leader != issueOwner) return false;
        var settlement = issueOwner.Clan.Settlements.FirstOrDefault(x => x.IsTown);
        if (settlement == null) return false;
        captured = Write(settlement);
        return true;
    }

    private byte[] Write(Settlement settlement)
    {
        if (settlement == null || !objects.TryGetIdWithLogging(settlement, out var id))
            throw new InvalidDataException("Revenue Farming target town is not registered.");
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(id);
        return stream.ToArray();
    }

    private static string Read(byte[] captured)
    {
        using var stream = new MemoryStream(captured);
        using var reader = new BinaryReader(stream);
        return reader.ReadString();
    }
}
