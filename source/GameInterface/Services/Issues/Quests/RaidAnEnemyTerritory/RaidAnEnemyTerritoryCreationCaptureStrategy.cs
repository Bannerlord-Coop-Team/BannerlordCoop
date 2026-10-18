using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

internal sealed class RaidAnEnemyTerritoryCreationCaptureStrategy : ICreationCaptureStrategy
{
    private static readonly FieldInfo EnemyKingdomField =
        HarmonyLib.AccessTools.Field(typeof(Issue), nameof(Issue._enemyKingdom));
    private readonly IObjectManager objectManager;

    public RaidAnEnemyTerritoryCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var raid = (Issue)issue;
        objectManager.TryGetIdWithLogging(raid._enemyKingdom, out var enemyKingdomId);
        return Write(enemyKingdomId ?? string.Empty);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        var enemyKingdomId = Read(captured);
        objectManager.TryGetObjectWithLogging<Kingdom>(enemyKingdomId, out var enemyKingdom);

        var issue = new Issue(issueOwner);
        EnemyKingdomField.SetValue(issue, enemyKingdom);
        return issue;
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if (issueOwner?.MapFaction == null)
        {
            return false;
        }

        var enemyKingdom = Campaign.Current.Kingdoms.FirstOrDefault(kingdom =>
            kingdom != issueOwner.MapFaction && kingdom.IsAtWarWith(issueOwner.MapFaction));
        if (enemyKingdom == null)
        {
            return false;
        }

        objectManager.TryGetIdWithLogging(enemyKingdom, out var enemyKingdomId);
        captured = Write(enemyKingdomId ?? string.Empty);
        return true;
    }

    private static byte[] Write(string enemyKingdomId)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(enemyKingdomId);
            writer.Flush();
            return stream.ToArray();
        }
    }

    private static string Read(byte[] captured)
    {
        using (var stream = new MemoryStream(captured ?? Array.Empty<byte>()))
        using (var reader = new BinaryReader(stream))
        {
            return reader.ReadString();
        }
    }
}
