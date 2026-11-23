using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons;

internal class ScoutEnemyGarrisonsCreationCaptureStrategy : ICreationCaptureStrategy
{
    private const int TargetCount = 3;

    private readonly IObjectManager objectManager;

    public ScoutEnemyGarrisonsCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var scoutIssue = (Issue)issue;

        return Write(new[] { scoutIssue._settlement1, scoutIssue._settlement2, scoutIssue._settlement3 });
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        // Vanilla's CreateNewIssue cannot skip a missing issue, so fail loudly instead of returning null
        if (!TryReadTargets(captured, out var targets))
        {
            throw new InvalidOperationException($"The scout targets of {issueOwner.StringId}'s issue could not be resolved");
        }

        return new Issue(issueOwner, targets);
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        if (issueOwner.MapFaction == null || !issueOwner.MapFaction.IsKingdomFaction)
        {
            return false;
        }

        // Like vanilla all targets belong to one enemy kingdom, the closest ones so the quest can be played without crossing the map
        var targets = Kingdom.All
            .Where(kingdom => kingdom.IsAtWarWith(issueOwner.MapFaction))
            .Select(kingdom => kingdom.Settlements
                .Where(settlement => ScoutEnemyGarrisonsIssueBehavior.SuitableSettlementCondition(settlement, issueOwner))
                .OrderBy(settlement => DistanceTo(issueOwner, settlement))
                .Take(TargetCount)
                .ToList())
            .Where(settlements => settlements.Count == TargetCount)
            .OrderBy(settlements => DistanceTo(issueOwner, settlements[0]))
            .FirstOrDefault();

        if (targets == null)
        {
            return false;
        }

        captured = Write(targets);
        return captured.Length > 0;
    }

    public bool TryReadTargets(byte[] captured, out List<Settlement> targets)
    {
        targets = new List<Settlement>();

        if (captured == null || captured.Length == 0)
        {
            return false;
        }

        try
        {
            using (var stream = new MemoryStream(captured))
            {
                using (var reader = new BinaryReader(stream))
                {
                    for (var i = 0; i < TargetCount; i++)
                    {
                        if (stream.Position >= stream.Length ||
                            !objectManager.TryGetObjectWithLogging<Settlement>(reader.ReadString(), out var target))
                        {
                            return false;
                        }

                        targets.Add(target);
                    }
                }
            }
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }

        return true;
    }

    private byte[] Write(IEnumerable<Settlement> targets)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                foreach (var target in targets)
                {
                    if (!objectManager.TryGetIdWithLogging(target, out var targetId))
                    {
                        return Array.Empty<byte>();
                    }

                    writer.Write(targetId);
                }

                writer.Flush();

                return stream.ToArray();
            }
        }
    }

    private static float DistanceTo(Hero hero, Settlement settlement)
    {
        var distanceModel = Campaign.Current.Models.MapDistanceModel;

        if (hero.CurrentSettlement != null)
        {
            return distanceModel.GetDistance(hero.CurrentSettlement, settlement, false, false, MobileParty.NavigationType.All);
        }

        if (hero.PartyBelongedTo != null)
        {
            return distanceModel.GetDistance(hero.PartyBelongedTo, settlement, false, MobileParty.NavigationType.All, out _);
        }

        return float.MaxValue;
    }
}
