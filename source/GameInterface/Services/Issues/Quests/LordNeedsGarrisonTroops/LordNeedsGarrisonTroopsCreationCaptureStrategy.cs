using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue;

namespace GameInterface.Services.Issues.Quests.LordNeedsGarrisonTroops;

internal class LordNeedsGarrisonTroopsCreationCaptureStrategy : ICreationCaptureStrategy
{
    private readonly IObjectManager objectManager;

    public LordNeedsGarrisonTroopsCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var garrisonIssue = (Issue)issue;
        return Write(IdOf(garrisonIssue._settlement), IdOf(garrisonIssue._neededTroopType));
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        if (!TryRead(captured, out var settlement, out var troop))
        {
            throw new InvalidOperationException($"The garrison issue for {issueOwner.StringId} has unresolved creation data");
        }

        return new Issue(issueOwner, settlement)
        {
            _neededTroopType = troop
        };
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        if (issueOwner == null || !issueOwner.IsLord || issueOwner.IsMinorFactionHero ||
            issueOwner.Clan == null || issueOwner.Clan.Leader != issueOwner || issueOwner.Clan == Clan.PlayerClan)
        {
            return false;
        }

        foreach (var settlement in issueOwner.Clan.Settlements)
        {
            var garrison = settlement.Town?.GarrisonParty;
            var limit = settlement.IsCastle ? 160 : settlement.IsTown ? 200 : 0;
            if (garrison == null || limit == 0 || garrison.MemberRoster.TotalRegulars >= limit)
            {
                continue;
            }

            var issue = new Issue(issueOwner, settlement);
            if (!issue.IssueStayAliveConditions())
            {
                continue;
            }

            captured = Capture(issue);
            return captured.Length > 0;
        }

        return false;
    }

    private string IdOf(object value)
    {
        return value != null && objectManager.TryGetIdWithLogging(value, out var id) ? id : string.Empty;
    }

    internal bool TryRead(byte[] captured, out Settlement settlement, out CharacterObject troop)
    {
        settlement = null;
        troop = null;
        if (captured == null || captured.Length == 0)
        {
            return false;
        }

        try
        {
            using (var stream = new MemoryStream(captured))
            using (var reader = new BinaryReader(stream))
            {
                if (stream.Position >= stream.Length)
                {
                    return false;
                }

                var settlementId = reader.ReadString();
                if (stream.Position >= stream.Length)
                {
                    return false;
                }

                var troopId = reader.ReadString();
                return objectManager.TryGetObjectWithLogging(settlementId, out settlement) &&
                    objectManager.TryGetObjectWithLogging(troopId, out troop);
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
    }

    private static byte[] Write(string settlementId, string troopId)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(settlementId);
            writer.Write(troopId);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
