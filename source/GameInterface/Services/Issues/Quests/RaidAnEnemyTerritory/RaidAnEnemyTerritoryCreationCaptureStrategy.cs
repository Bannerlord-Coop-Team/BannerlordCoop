using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

internal sealed class RaidAnEnemyTerritoryCreationCaptureStrategy : ICreationCaptureStrategy
{
    // The only vanilla constructor rolls this readonly field, so restore the captured roll afterward.
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
        if (!objectManager.TryGetIdWithLogging(raid._enemyKingdom, out var enemyKingdomId))
        {
            throw new InvalidOperationException("The raid issue enemy kingdom is not registered");
        }

        return Encoding.UTF8.GetBytes(enemyKingdomId);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        var enemyKingdomId = Encoding.UTF8.GetString(captured);
        if (!objectManager.TryGetObjectWithLogging<Kingdom>(enemyKingdomId, out var enemyKingdom))
        {
            throw new InvalidOperationException("The raid issue enemy kingdom could not be resolved");
        }

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
        if (enemyKingdom == null || !objectManager.TryGetIdWithLogging(enemyKingdom, out var enemyKingdomId))
        {
            return false;
        }

        captured = Encoding.UTF8.GetBytes(enemyKingdomId);
        return true;
    }
}
