using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentSpawnManifestValidatorTests
{
    [Fact]
    public void IsValid_EnforcesCanonicalHumanAndNpcOwnership()
    {
        TournamentSessionSnapshot snapshot = CreateSnapshot();
        TournamentSpawnManifestData valid = CreateManifest(snapshot, "player", "host");

        Assert.True(TournamentSpawnManifestValidator.IsValid(valid, snapshot));
        Assert.False(TournamentSpawnManifestValidator.IsValid(
            CreateManifest(snapshot, "host", "host"),
            snapshot));
        Assert.False(TournamentSpawnManifestValidator.IsValid(
            CreateManifest(snapshot, "player", "outsider"),
            snapshot));
    }

    [Fact]
    public void IsValid_RejectsDuplicateAgentIdentityAndWrongTeamBanner()
    {
        TournamentSessionSnapshot snapshot = CreateSnapshot();
        TournamentSpawnManifestData valid = CreateManifest(snapshot, "player", "host");
        TournamentAgentSpawnData first = valid.Agents[0];
        TournamentAgentSpawnData second = valid.Agents[1];
        valid.Agents[1] = new TournamentAgentSpawnData(
            first.AgentId,
            second.SlotId,
            second.CharacterId,
            second.DescriptorSeed,
            second.TeamId,
            second.TeamColor,
            "wrong-banner",
            second.ControllerId,
            second.Equipment,
            second.PositionX,
            second.PositionY,
            second.PositionZ,
            second.DirectionX,
            second.DirectionY,
            second.Health,
            second.MountAgentId,
            second.MountCharacterId,
            second.MountDescriptorSeed,
            second.MountEquipment,
            second.MountHealth);

        Assert.False(TournamentSpawnManifestValidator.IsValid(valid, snapshot));
    }

    [Fact]
    public void IsValid_AllowsMountCharacterDerivedFromSynchronizedHorseEquipment()
    {
        TournamentSessionSnapshot snapshot = CreateSnapshot();
        TournamentSpawnManifestData manifest = CreateManifest(snapshot, "player", "host");
        TournamentAgentSpawnData npc = manifest.Agents[1];
        manifest.Agents[1] = new TournamentAgentSpawnData(
            npc.AgentId,
            npc.SlotId,
            npc.CharacterId,
            npc.DescriptorSeed,
            npc.TeamId,
            npc.TeamColor,
            npc.TeamBannerCode,
            npc.ControllerId,
            npc.Equipment,
            npc.PositionX,
            npc.PositionY,
            npc.PositionZ,
            npc.DirectionX,
            npc.DirectionY,
            npc.Health,
            Guid.NewGuid(),
            null,
            77,
            new[] { new TournamentEquipmentElementData(10, "horse", null) },
            90);

        Assert.True(TournamentSpawnManifestValidator.IsValid(manifest, snapshot));
    }
    private static TournamentSessionSnapshot CreateSnapshot()
    {
        var contestants = new[]
        {
            new TournamentContestantData("human", "hero", 1, "player", "Player", true, false, true, null),
            new TournamentContestantData("npc", "troop", 2, null, "Troop", false, false, false, null)
        };
        var teams = new[]
        {
            new TournamentTeamData("team-a", new[] { "human" }, 0, false, 1, "banner-a"),
            new TournamentTeamData("team-b", new[] { "npc" }, 0, false, 2, "banner-b")
        };
        var match = new TournamentMatchData("match", "round", 0, 1, 1, teams, Array.Empty<string>(), 1);
        return new TournamentSessionSnapshot(
            "session", "mission", "town", "arena", "prize",
            TournamentSessionPhase.LiveMatch, 4, 2, "match", "host", Array.Empty<string>(),
            contestants, Array.Empty<string>(), Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData("round", 0, 0, new[] { match }) },
            0, 0, 2, false, false, null);
    }

    private static TournamentSpawnManifestData CreateManifest(
        TournamentSessionSnapshot snapshot,
        string humanOwner,
        string npcOwner)
    {
        TournamentTeamData[] teams = snapshot.Rounds[0].Matches[0].Teams;
        var equipment = new[] { new TournamentEquipmentElementData(0, "weapon", null) };
        var agents = new[]
        {
            new TournamentAgentSpawnData(
                Guid.NewGuid(), "human", "hero", 1, teams[0].TeamId, teams[0].TeamColor,
                teams[0].BannerCode, humanOwner, equipment, 0, 0, 0, 0, 1, 100,
                Guid.Empty, null, 0, Array.Empty<TournamentEquipmentElementData>(), 0),
            new TournamentAgentSpawnData(
                Guid.NewGuid(), "npc", "troop", 2, teams[1].TeamId, teams[1].TeamColor,
                teams[1].BannerCode, npcOwner, equipment, 1, 0, 0, 0, 1, 100,
                Guid.Empty, null, 0, Array.Empty<TournamentEquipmentElementData>(), 0)
        };
        return new TournamentSpawnManifestData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            1,
            agents);
    }
}
