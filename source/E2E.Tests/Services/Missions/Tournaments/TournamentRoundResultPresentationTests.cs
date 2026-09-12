using GameInterface.Services.Tournaments.Data;
using Missions.Tournaments;
using Missions.Tournaments.Messages;
using System;
using Xunit;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentRoundResultPresentationTests
{
    [Fact]
    public void TeamWinner_GetsNativeQualifiedMessage()
    {
        var result = CreateResult(new[] { "slot-a", "slot-b" }, false, true);

        string text = TournamentRoundResultPresentation.GetText(
            CreateSnapshot(),
            "player-a",
            result);

        Assert.Equal(
            "{=fkOYvnVG}Round is over, your team is qualified for the next stage of the tournament.",
            text);
    }

    [Fact]
    public void TeamLoser_GetsNativeDisqualifiedMessage()
    {
        var result = CreateResult(new[] { "slot-c", "slot-d" }, false, true);

        string text = TournamentRoundResultPresentation.GetText(
            CreateSnapshot(),
            "player-a",
            result);

        Assert.Equal(
            "{=MLyBN51z}Round is over, your team is disqualified from the tournament.",
            text);
    }

    [Fact]
    public void Spectator_GetsNativeMatchOverMessage()
    {
        var result = CreateResult(new[] { "slot-a", "slot-b" }, false, true);

        string text = TournamentRoundResultPresentation.GetText(
            CreateSnapshot(),
            "spectator-a",
            result);

        Assert.Equal("{=UBd0dEPp}Match is over", text);
    }

    private static NetworkTournamentRoundEnded CreateResult(
        string[] winnerSlotIds,
        bool isLastRound,
        bool isTeamQualification)
        => new(
            "session-a",
            "match-a",
            1,
            "host-a",
            winnerSlotIds,
            isLastRound,
            isTeamQualification);

    private static TournamentSessionSnapshot CreateSnapshot()
    {
        var match = new TournamentMatchData(
            "match-a",
            "round-a",
            0,
            2,
            1,
            new[]
            {
                new TournamentTeamData("team-a", new[] { "slot-a", "slot-b" }, 0, false, 0, null),
                new TournamentTeamData("team-b", new[] { "slot-c", "slot-d" }, 0, false, 0, null)
            },
            Array.Empty<string>());
        return new TournamentSessionSnapshot(
            "session-a",
            "mission-a",
            "town-a",
            "arena-a",
            "prize-a",
            TournamentSessionPhase.LiveMatch,
            1,
            1,
            "match-a",
            "host-a",
            Array.Empty<string>(),
            new[]
            {
                new TournamentContestantData("slot-a", "character-a", 1, "player-a", "Player A", true, false, true, "npc-a"),
                new TournamentContestantData("slot-b", "character-b", 2, "player-b", "Player B", true, false, true, "npc-b"),
                new TournamentContestantData("slot-c", "character-c", 3, null, "AI C", false, false, true, "npc-c"),
                new TournamentContestantData("slot-d", "character-d", 4, null, "AI D", false, false, true, "npc-d")
            },
            new[] { "spectator-a" },
            Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData("round-a", 0, 0, new[] { match }) },
            0,
            0,
            0,
            false,
            false,
            null);
    }
}