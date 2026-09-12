using GameInterface.Services.Tournaments.Data;
using ProtoBuf;
using System;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentSessionSnapshotNormalizerTests
{
    [Fact]
    public void Normalize_RestoresEmptyCollectionsOmittedByProtobuf()
    {
        var contestant = new TournamentContestantData(
            "slot-a",
            "character-a",
            1,
            "controller-a",
            "Player A",
            true,
            false,
            true,
            null);
        var team = new TournamentTeamData(
            "team-a",
            new[] { contestant.SlotId },
            0,
            false,
            0,
            null);
        var match = new TournamentMatchData(
            "match-a",
            "round-a",
            0,
            1,
            1,
            new[] { team },
            Array.Empty<string>());
        var snapshot = new TournamentSessionSnapshot(
            "session-a",
            "mission-a",
            "town-a",
            "arena-a",
            "prize-a",
            TournamentSessionPhase.AwaitingChoices,
            2,
            1,
            match.MatchId,
            contestant.ControllerId,
            Array.Empty<string>(),
            new[] { contestant },
            Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData("round-a", 0, 0, new[] { match }) },
            0,
            0,
            1,
            true,
            false,
            null);

        TournamentSessionSnapshot deserialized;
        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, snapshot);
            stream.Position = 0;
            deserialized = Serializer.Deserialize<TournamentSessionSnapshot>(stream);
        }

        Assert.Null(deserialized.SpectatorControllerIds);
        Assert.Null(deserialized.Rounds[0].Matches[0].WinnerSlotIds);

        TournamentSessionSnapshot normalized = TournamentSessionSnapshotNormalizer.Normalize(deserialized);

        Assert.Empty(normalized.SuccessorControllerIds);
        Assert.Empty(normalized.SpectatorControllerIds);
        Assert.Empty(normalized.Choices);
        Assert.Empty(normalized.Rounds[0].Matches[0].WinnerSlotIds);
        Assert.Equal(new[] { contestant.SlotId }, normalized.Rounds[0].Matches[0].Teams[0].ParticipantSlotIds);
    }
}
