using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;

internal sealed class TheConquestOfSettlementCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public TheConquestOfSettlementCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var conquest = (Issue)issue;
        objectManager.TryGetIdWithLogging(conquest._targetSettlement, out var targetId);
        return Write(targetId, conquest.IssueDueTime.NumTicks);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        Read(captured, out var targetId, out _);
        if (!objectManager.TryGetObjectWithLogging<Settlement>(targetId, out var target))
        {
            return null;
        }

        return new Issue(issueOwner, target);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
        Read(captured, out _, out var dueTicks);
        ((Issue)issue).IssueDueTime = new CampaignTime(dueTicks);
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if (issueOwner == null)
        {
            return false;
        }

        var behavior = Campaign.Current?.GetCampaignBehavior<TheConquestOfSettlementIssueBehavior>();
        if (behavior == null || !behavior.ConditionsHold(issueOwner, out var target) || target == null)
        {
            target = Campaign.Current?.AllTowns
                .Where(town => town.MapFaction != null && issueOwner.MapFaction != null &&
                    town.MapFaction.IsAtWarWith(issueOwner.MapFaction))
                .Select(town => town.Settlement)
                .FirstOrDefault();
        }

        if (target == null || !objectManager.TryGetId(target, out var targetId))
        {
            return false;
        }

        captured = Write(targetId, CampaignTime.DaysFromNow(60).NumTicks);
        return true;
    }

    private static byte[] Write(string targetId, long dueTicks)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(targetId ?? string.Empty);
        writer.Write(dueTicks);
        return stream.ToArray();
    }

    private static void Read(byte[] captured, out string targetId, out long dueTicks)
    {
        using var stream = new MemoryStream(captured ?? System.Array.Empty<byte>());
        using var reader = new BinaryReader(stream);
        targetId = reader.ReadString();
        dueTicks = reader.ReadInt64();
    }
}
