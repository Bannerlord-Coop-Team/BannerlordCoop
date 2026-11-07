using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentSpawnManifestDiagnosticsTests
{
    [Theory]
    [InlineData("valid", null)]
    [InlineData("character", "character or seed mismatch")]
    [InlineData("seed", "character or seed mismatch")]
    [InlineData("owner", "owner mismatch")]
    [InlineData("team", "team mismatch")]
    [InlineData("color", "team mismatch")]
    [InlineData("color2", "team mismatch")]
    [InlineData("banner", "team mismatch")]
    [InlineData("position", "non-finite position")]
    [InlineData("direction", "invalid direction")]
    [InlineData("health", "invalid health=0")]
    [InlineData("equipment", "invalid equipment length")]
    [InlineData("mount-character", "mount character without mount id")]
    [InlineData("mount-health", "invalid mount health=0")]
    [InlineData("mount-equipment", "invalid mount equipment length")]
    public void EightAgentManifest_ReportsTheRejectedSlotAndCheck(string fault, string? expectedReason)
    {
        TournamentSessionSnapshot snapshot = Snapshot();
        TournamentAgentSpawnData[] agents = Enumerable.Range(0, 8)
            .Select(index => Agent(snapshot, index, index == 3 ? fault : "valid"))
            .ToArray();
        var manifest = new TournamentSpawnManifestData(
            snapshot.SessionId, snapshot.CurrentMatchId, snapshot.Revision,
            snapshot.BracketRevision, 1, agents);

        bool accepted = TournamentSpawnManifestValidator.IsValid(manifest, snapshot, out string reason);

        Assert.Equal(expectedReason == null, accepted);
        Assert.Equal(accepted, TournamentSpawnManifestValidator.IsValid(manifest, snapshot));
        Assert.Equal(8, manifest.Agents.Length);
        if (expectedReason == null)
            Assert.Null(reason);
        else
        {
            Assert.StartsWith("slot=slot-3: ", reason);
            Assert.Contains(expectedReason, reason);
        }
    }

    private static TournamentAgentSpawnData Agent(TournamentSessionSnapshot snapshot, int index, string fault)
    {
        TournamentContestantData contestant = snapshot.Contestants[index];
        TournamentTeamData team = snapshot.Rounds[0].Matches[0].Teams[index / 4];
        bool mounted = fault is "mount-health" or "mount-equipment";
        return new TournamentAgentSpawnData(
            Guid.NewGuid(), contestant.SlotId,
            fault == "character" ? "other-character" : contestant.CharacterId,
            fault == "seed" ? -1 : contestant.DescriptorSeed,
            fault == "team" ? "other-team" : team.TeamId,
            fault == "color" ? 0 : team.TeamColor,
            fault == "color2" ? 0 : team.TeamColor2,
            fault == "banner" ? "other-banner" : team.BannerCode,
            fault == "owner" ? "other-controller" : index == 0 ? "fighter" : "host",
            fault == "equipment" ? OversizedEquipment() : Array.Empty<EquipmentElement>(),
            fault == "position" ? new Vec3(float.NaN, 0, 0) : new Vec3(index, 0, 0),
            fault == "direction" ? Vec2.Zero : new Vec2(0, 1),
            fault == "health" ? 0 : 100,
            mounted ? Guid.NewGuid() : Guid.Empty,
            fault == "mount-character" ? "horse" : null,
            0,
            fault == "mount-equipment" ? OversizedEquipment() : Array.Empty<EquipmentElement>(),
            fault == "mount-health" ? 0 : mounted ? 100 : 0);
    }

    private static EquipmentElement[] OversizedEquipment()
        => new EquipmentElement[(int)EquipmentIndex.NumEquipmentSetSlots + 1];

    private static TournamentSessionSnapshot Snapshot()
    {
        TournamentContestantData[] contestants = Enumerable.Range(0, 8)
            .Select(index => new TournamentContestantData(
                $"slot-{index}", $"character-{index}", index + 1,
                index == 0 ? "fighter" : null, $"Contestant {index}",
                index == 0, false, index == 0, null))
            .ToArray();
        TournamentTeamData[] teams = Enumerable.Range(0, 2)
            .Select(index => new TournamentTeamData(
                $"team-{index}", contestants.Skip(index * 4).Take(4).Select(contestant => contestant.SlotId).ToArray(),
                0, false, (uint)(index + 1), (uint)(index + 3), $"banner-{index}"))
            .ToArray();
        var match = new TournamentMatchData(
            "round:0:match:0", "round:0", 4, 4, 1, teams, Array.Empty<string>(), 1);
        return new TournamentSessionSnapshot(
            "session", "mission", "town", "arena", "prize",
            TournamentSessionPhase.LiveMatch, 7, 1, match.MatchId, "host", Array.Empty<string>(),
            contestants, Array.Empty<string>(), Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData("round:0", 0, 0, new[] { match }) },
            0, 0, 2, false, false, null);
    }
}
