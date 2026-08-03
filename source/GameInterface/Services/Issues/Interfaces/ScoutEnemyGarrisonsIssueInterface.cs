using Common.Util;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.ScoutEnemyGarrisonsIssueCreationPatch"/>/
/// <see cref="Handlers.ScoutEnemyGarrisonsIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue"/>. The three target settlements are
/// picked deterministically (nearest-by-distance, no MBRandom) BEFORE construction and handed in via
/// <c>PotentialIssueData.RelatedObject</c> - same shape as Captured by Bounty Hunters' hideout - but still need
/// broadcasting as object references so every peer's replicated copy targets the exact same three settlements.
///
/// Reward is hardcoded 0 and resolution needs no accept-time re-derivation (matches the survey), but
/// completion is driven entirely by the QUEST's own ambient <c>HourlyTick</c> checking the LOCAL
/// <c>MobileParty.MainParty</c>'s distance to each target settlement - a wrinkle the survey missed: since
/// every peer's own mirrored quest object runs its own <c>HourlyTick</c>, a non-owner peer who happens to
/// travel near the same (globally shared, faction-owned) enemy settlements would otherwise silently advance
/// and complete someone else's scouting quest. See <see cref="Patches.ScoutEnemyGarrisonsOwnershipGatePatch"/>.
/// </summary>
public interface IScoutEnemyGarrisonsIssueInterface : IGameAbstraction
{
    bool TryCaptureFields(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue issue, out Settlement settlement1, out Settlement settlement2, out Settlement settlement3);

    ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue ConstructReplicated(Hero owner, Settlement settlement1, Settlement settlement2, Settlement settlement3);

    void RegisterReplicated(Hero owner, ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue issue);
}

/// <inheritdoc cref="IScoutEnemyGarrisonsIssueInterface"/>
public class ScoutEnemyGarrisonsIssueInterface : IScoutEnemyGarrisonsIssueInterface
{
    private static readonly FieldInfo Settlement1Field =
        AccessTools.Field(typeof(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue), "_settlement1");
    private static readonly FieldInfo Settlement2Field =
        AccessTools.Field(typeof(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue), "_settlement2");
    private static readonly FieldInfo Settlement3Field =
        AccessTools.Field(typeof(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue), "_settlement3");

    public bool TryCaptureFields(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue issue, out Settlement settlement1, out Settlement settlement2, out Settlement settlement3)
    {
        settlement1 = null;
        settlement2 = null;
        settlement3 = null;
        if (issue == null) return false;

        settlement1 = (Settlement)Settlement1Field.GetValue(issue);
        settlement2 = (Settlement)Settlement2Field.GetValue(issue);
        settlement3 = (Settlement)Settlement3Field.GetValue(issue);
        return settlement1 != null && settlement2 != null && settlement3 != null;
    }

    public ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue ConstructReplicated(Hero owner, Settlement settlement1, Settlement settlement2, Settlement settlement3)
    {
        var settlements = new List<Settlement> { settlement1, settlement2, settlement3 };
        var issue = new ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue(owner, settlements);

        Settlement1Field.SetValue(issue, settlement1);
        Settlement2Field.SetValue(issue, settlement2);
        Settlement3Field.SetValue(issue, settlement3);

        return issue;
    }

    public void RegisterReplicated(Hero owner, ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
