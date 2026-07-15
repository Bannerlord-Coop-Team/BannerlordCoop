using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentSessionRegistryTests
{
    [Fact]
    public void Join_ReplacesLowestPriorityNonLordBeforeLord()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateSession(registry, "session-1", "town-1");

        var status = registry.TryJoin(
            snapshot.SessionId,
            snapshot.Revision,
            "player-1",
            "player-character",
            "Player One",
            500,
            true,
            out snapshot);

        Assert.Equal(TournamentMutationStatus.Applied, status);
        TournamentContestantData player = Assert.Single(snapshot.Contestants.Where(contestant => contestant.IsHuman));
        Assert.Equal("session-1:slot:14", player.SlotId);
        Assert.Equal("npc-14", player.DisplacedCharacterId);
        Assert.False(snapshot.Contestants.Single(contestant => contestant.SlotId == "session-1:slot:15").IsHuman);
    }

    [Fact]
    public void Join_RejectsSeventeenthHuman()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateSession(registry, "session-1", "town-1");

        for (int i = 0; i < TournamentSessionRegistry.MaximumCompetitorCount; i++)
        {
            Assert.Equal(TournamentMutationStatus.Applied, registry.TryJoin(
                snapshot.SessionId,
                snapshot.Revision,
                $"player-{i}",
                $"player-character-{i}",
                $"Player {i}",
                500 + i,
                false,
                out snapshot));
        }

        Assert.Equal(TournamentMutationStatus.Full, registry.TryJoin(
            snapshot.SessionId,
            snapshot.Revision,
            "player-17",
            "player-character-17",
            "Player 17",
            999,
            false,
            out _));
    }

    [Fact]
    public void LastPreparationLeave_RemovesSessionAndRestoresNativeTournamentAvailability()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateSession(registry, "session-1", "town-1");
        registry.TryJoin(
            snapshot.SessionId,
            snapshot.Revision,
            "player-1",
            "player-character",
            "Player One",
            500,
            false,
            out snapshot);

        var status = registry.TryLeavePreparation(
            snapshot.SessionId,
            snapshot.Revision,
            "player-1",
            out var removedSnapshot,
            out var removed);

        Assert.Equal(TournamentMutationStatus.Applied, status);
        Assert.True(removed);
        Assert.Null(removedSnapshot);
        Assert.False(registry.TryGetByTown("town-1", out _));
    }

    [Fact]
    public void Registry_AllowsConcurrentSessionsInDifferentTowns()
    {
        var registry = new TournamentSessionRegistry();

        CreateSession(registry, "session-1", "town-1");
        CreateSession(registry, "session-2", "town-2");

        Assert.Equal(2, registry.GetAll().Length);
        Assert.True(registry.TryGetByTown("town-1", out var first));
        Assert.True(registry.TryGetByTown("town-2", out var second));
        Assert.NotEqual(first.SessionId, second.SessionId);
    }

    [Fact]
    public void StaleRevision_DoesNotMutateSession()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateSession(registry, "session-1", "town-1");
        registry.TryJoin(
            snapshot.SessionId,
            snapshot.Revision,
            "player-1",
            "player-character",
            "Player One",
            500,
            false,
            out snapshot);

        var status = registry.TryJoin(
            snapshot.SessionId,
            snapshot.Revision - 1,
            "player-2",
            "player-character-2",
            "Player Two",
            501,
            false,
            out var canonical);

        Assert.Equal(TournamentMutationStatus.StaleRevision, status);
        Assert.Equal(snapshot.Revision, canonical.Revision);
        Assert.Single(canonical.Contestants.Where(contestant => contestant.IsHuman));
    }

    [Fact]
    public void HumanMatch_DisablesSkipAndRequiresAllVotersReady()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: true);

        Assert.False(snapshot.SkipAllowed);
        Assert.Equal(TournamentMutationStatus.InvalidChoice, registry.TryChoose(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            "player-2",
            TournamentPlayerChoice.Skip,
            out _,
            out _));

        Assert.Equal(TournamentMutationStatus.InvalidChoice, registry.TryChoose(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            "player-1",
            TournamentPlayerChoice.Watch,
            out _,
            out _));

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryChoose(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            "player-1",
            TournamentPlayerChoice.Join,
            out snapshot,
            out var firstOutcome));
        Assert.Equal(TournamentBallotOutcome.Open, firstOutcome);

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryChoose(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            "player-2",
            TournamentPlayerChoice.Watch,
            out snapshot,
            out var outcome));
        Assert.Equal(TournamentBallotOutcome.StartLiveMatch, outcome);
        Assert.Equal(TournamentSessionPhase.LiveMatch, snapshot.Phase);
    }

    [Fact]
    public void LateSpectator_JoinsOpenBallotImmediately()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: false);

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryRequestSpectate(
            snapshot.SessionId,
            snapshot.Revision,
            "spectator",
            out snapshot));
        Assert.Equal(3, snapshot.VoterCount);
        Assert.Contains("spectator", snapshot.SpectatorControllerIds);

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryChoose(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            "spectator",
            TournamentPlayerChoice.Watch,
            out snapshot,
            out var outcome));
        Assert.Equal(TournamentBallotOutcome.Open, outcome);
        Assert.Equal(1, snapshot.ReadyCount);

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryEnterMission(
            snapshot.SessionId,
            snapshot.Revision,
            "spectator",
            out snapshot));

        Assert.Equal(3, snapshot.VoterCount);
        Assert.Contains("spectator", snapshot.SpectatorControllerIds);
        Assert.Equal("spectator", snapshot.HostControllerId);
    }

    [Fact]
    public void ConcurrentMissionEntries_AcceptSameStartingRevision()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: true);
        long startingRevision = snapshot.Revision;

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryEnterMission(
            snapshot.SessionId,
            startingRevision,
            "player-1",
            out snapshot));
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryEnterMission(
            snapshot.SessionId,
            startingRevision,
            "player-2",
            out snapshot));

        Assert.Equal("player-1", snapshot.HostControllerId);
        Assert.Contains("player-2", snapshot.SuccessorControllerIds);
    }

    [Fact]
    public void ChoicesRemainMutableUntilUnanimousSkip()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: false);

        registry.TryChoose(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            "player-1",
            TournamentPlayerChoice.Skip,
            out snapshot,
            out _);
        registry.TryChoose(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            "player-1",
            TournamentPlayerChoice.Watch,
            out snapshot,
            out _);
        registry.TryChoose(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            "player-2",
            TournamentPlayerChoice.Skip,
            out snapshot,
            out var mixedOutcome);

        Assert.Equal(TournamentBallotOutcome.Open, mixedOutcome);
        Assert.Equal(TournamentSessionPhase.AwaitingChoices, snapshot.Phase);

        registry.TryChoose(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            "player-1",
            TournamentPlayerChoice.Skip,
            out snapshot,
            out var outcome);
        Assert.Equal(TournamentBallotOutcome.SimulateMatch, outcome);
        Assert.Equal(TournamentSessionPhase.SimulatingMatch, snapshot.Phase);
    }

    [Fact]
    public void ConcurrentChoices_AcceptSameStartingRevision()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: true);
        long startingRevision = snapshot.Revision;

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryChoose(
            snapshot.SessionId,
            startingRevision,
            snapshot.CurrentMatchId,
            "player-1",
            TournamentPlayerChoice.Join,
            out snapshot,
            out var firstOutcome));
        Assert.Equal(TournamentBallotOutcome.Open, firstOutcome);

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryChoose(
            snapshot.SessionId,
            startingRevision,
            snapshot.CurrentMatchId,
            "player-2",
            TournamentPlayerChoice.Watch,
            out snapshot,
            out var secondOutcome));
        Assert.Equal(TournamentBallotOutcome.StartLiveMatch, secondOutcome);
        Assert.Equal(2, snapshot.ReadyCount);
    }

    [Fact]
    public void ActiveLeave_PermanentlyReplacesHumanAndPromotesHostSuccessor()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: true);
        registry.TryEnterMission(snapshot.SessionId, snapshot.Revision, "player-1", out snapshot);
        registry.TryEnterMission(snapshot.SessionId, snapshot.Revision, "player-2", out snapshot);

        Assert.Equal("player-1", snapshot.HostControllerId);
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryLeaveActive(
            snapshot.SessionId,
            snapshot.Revision,
            "player-1",
            900,
            "Tournament Recruit",
            out snapshot,
            out _,
            out _));

        Assert.Equal("player-2", snapshot.HostControllerId);
        TournamentContestantData replacement = snapshot.Contestants.Single(contestant => contestant.IsReplaced);
        Assert.Equal("basic-troop", replacement.CharacterId);
        Assert.Null(replacement.ControllerId);
        Assert.False(replacement.IsHuman);
    }

    [Fact]
    public void MissionAdmission_RequiresExactActiveSessionAndEnrolledController()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: true);

        Assert.True(registry.CanEnterMission(snapshot.MissionInstanceId, "player-1"));
        Assert.False(registry.CanEnterMission(snapshot.MissionInstanceId, "outsider"));
        Assert.False(registry.CanEnterMission("another-instance", "player-1"));

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryRequestSpectate(
            snapshot.SessionId,
            snapshot.Revision,
            "spectator",
            out snapshot));
        Assert.True(registry.CanEnterMission(snapshot.MissionInstanceId, "spectator"));
    }

    [Fact]
    public void SpawnManifest_RejectsSessionOrBracketRevisionMismatch()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateLiveSession(registry);
        TournamentSpawnManifestData valid = CreateManifest(snapshot, snapshot.Revision, snapshot.BracketRevision);

        Assert.Equal(TournamentMutationStatus.StaleRevision, registry.TryStoreSpawnManifest(
            CreateManifest(snapshot, snapshot.Revision - 1, snapshot.BracketRevision),
            snapshot.HostControllerId,
            out _));
        Assert.Equal(TournamentMutationStatus.Rejected, registry.TryStoreSpawnManifest(
            CreateManifest(snapshot, snapshot.Revision, snapshot.BracketRevision + 1),
            snapshot.HostControllerId,
            out _));
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryStoreSpawnManifest(
            valid,
            snapshot.HostControllerId,
            out _));
    }

    [Fact]
    public void Join_RejectsControllerAlreadyCompetingInAnotherTown()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot first = CreateSession(registry, "session-1", "town-1");
        TournamentSessionSnapshot second = CreateSession(registry, "session-2", "town-2");

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryJoin(
            first.SessionId,
            first.Revision,
            "player-1",
            "player-character",
            "Player One",
            500,
            false,
            out first));
        Assert.Equal(TournamentMutationStatus.Rejected, registry.TryJoin(
            second.SessionId,
            second.Revision,
            "player-1",
            "player-character",
            "Player One",
            500,
            false,
            out second));
        Assert.DoesNotContain(second.Contestants, contestant => contestant.IsHuman);
    }

    [Fact]
    public void ForceSimulation_PreservesHumanSlotsForOrderlyShutdown()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: true);

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryForceSimulation(
            snapshot.SessionId,
            snapshot.Revision,
            out snapshot));

        Assert.Equal(TournamentSessionPhase.SimulatingMatch, snapshot.Phase);
        Assert.Equal(2, snapshot.Contestants.Count(contestant => contestant.IsHuman));
        Assert.DoesNotContain(snapshot.Contestants, contestant => contestant.IsReplaced);
    }

    [Fact]
    public void ActiveLeave_ReevaluatesDepartureDenominator()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: false);
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryChoose(
            snapshot.SessionId,
            snapshot.Revision,
            snapshot.CurrentMatchId,
            "player-1",
            TournamentPlayerChoice.Skip,
            out snapshot,
            out _));

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryLeaveActive(
            snapshot.SessionId,
            snapshot.Revision,
            "player-2",
            900,
            "Tournament Recruit",
            out snapshot,
            out var outcome,
            out _));

        Assert.Equal(TournamentBallotOutcome.SimulateMatch, outcome);
        Assert.Equal(TournamentSessionPhase.SimulatingMatch, snapshot.Phase);
    }

    [Fact]
    public void MatchResult_PersistsCanonicalContestantScores()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateLiveSession(registry);
        var scores = snapshot.Contestants.ToDictionary(
            contestant => contestant.SlotId,
            contestant => contestant.SlotId == snapshot.Contestants[0].SlotId ? 7 : contestant.Score);
        var result = new TournamentMatchResultData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            1,
            new[] { snapshot.Rounds[0].Matches[0].Teams[0].TeamId },
            snapshot.Rounds[0].Matches[0].Teams[0].ParticipantSlotIds,
            new[]
            {
                new TournamentTeamScoreData(snapshot.Rounds[0].Matches[0].Teams[0].TeamId, 1),
                new TournamentTeamScoreData(snapshot.Rounds[0].Matches[0].Teams[1].TeamId, 0)
            });

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryApplyMatchResult(
            result,
            snapshot.HostControllerId,
            snapshot.Rounds,
            scores,
            "next-match",
            null,
            false,
            out snapshot));
        Assert.Equal(7, snapshot.Contestants[0].Score);
    }

    [Fact]
    public void MatchResult_RejectsBracketRevisionMismatch()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateLiveSession(registry);
        var result = new TournamentMatchResultData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision + 1,
            1,
            new[] { snapshot.Rounds[0].Matches[0].Teams[0].TeamId },
            snapshot.Rounds[0].Matches[0].Teams[0].ParticipantSlotIds,
            Array.Empty<TournamentTeamScoreData>());

        Assert.Equal(TournamentMutationStatus.Rejected, registry.TryApplyMatchResult(
            result,
            snapshot.HostControllerId,
            snapshot.Rounds,
            new Dictionary<string, int>(),
            null,
            null,
            false,
            out _));
    }

    [Fact]
    public void Start_ClosesRosterAgainstLateJoin()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: true);

        Assert.Equal(TournamentMutationStatus.InvalidPhase, registry.TryJoin(
            snapshot.SessionId,
            snapshot.Revision,
            "late-player",
            "late-character",
            "Late Player",
            700,
            false,
            out _));
    }

    [Fact]
    public void ActiveLeave_DoesNotReplaceSameControllerTwice()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: true);

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryLeaveActive(
            snapshot.SessionId,
            snapshot.Revision,
            "player-1",
            900,
            "Tournament Recruit",
            out snapshot,
            out _,
            out _));
        Assert.Equal(TournamentMutationStatus.NoChange, registry.TryLeaveActive(
            snapshot.SessionId,
            snapshot.Revision,
            "player-1",
            901,
            "Another Recruit",
            out snapshot,
            out _,
            out _));

        Assert.Single(snapshot.Contestants.Where(contestant => contestant.IsReplaced));
    }

    [Fact]
    public void MatchResult_RejectsAuthenticatedNonHost()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot snapshot = CreateLiveSession(registry);
        var result = new TournamentMatchResultData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            1,
            new[] { snapshot.Rounds[0].Matches[0].Teams[0].TeamId },
            snapshot.Rounds[0].Matches[0].Teams[0].ParticipantSlotIds,
            new[]
            {
                new TournamentTeamScoreData(snapshot.Rounds[0].Matches[0].Teams[0].TeamId, 1),
                new TournamentTeamScoreData(snapshot.Rounds[0].Matches[0].Teams[1].TeamId, 0)
            });
        Dictionary<string, int> scores = snapshot.Contestants.ToDictionary(
            contestant => contestant.SlotId,
            contestant => contestant.Score);

        Assert.Equal(TournamentMutationStatus.Rejected, registry.TryApplyMatchResult(
            result,
            "player-2",
            snapshot.Rounds,
            scores,
            null,
            null,
            true,
            out _));
    }

    [Fact]
    public void Remove_AllowsSequentialSessionInSameTown()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot first = CreateSession(registry, "session-1", "town-1");

        Assert.True(registry.Remove(first.SessionId));
        TournamentSessionSnapshot second = CreateSession(registry, "session-2", "town-1");

        Assert.Equal("session-2", second.SessionId);
        Assert.True(registry.TryGetByTown("town-1", out var current));
        Assert.Equal(second.SessionId, current.SessionId);
    }

    [Fact]
    public void HasActiveSessions_DoesNotPersistPreparationButBlocksStartedTournamentSave()
    {
        var registry = new TournamentSessionRegistry();
        TournamentSessionSnapshot preparation = CreateSession(registry, "session-1", "town-1");
        Assert.False(registry.HasActiveSessions);

        registry.Remove(preparation.SessionId);
        CreateStartedSession(registry, humanInMatch: true);

        Assert.True(registry.HasActiveSessions);
    }

    private static TournamentSessionSnapshot CreateSession(
        TournamentSessionRegistry registry,
        string sessionId,
        string townId)
    {
        TournamentContestantData[] contestants = Enumerable.Range(0, 16)
            .Select(index => new TournamentContestantData(
                $"{sessionId}:slot:{index}",
                $"npc-{index}",
                index + 1,
                null,
                $"NPC {index}",
                false,
                false,
                index == 15,
                null,
                0))
            .ToArray();
        var seed = new TournamentSessionSeed(
            sessionId,
            sessionId,
            townId,
            "arena-scene",
            "prize-item",
            "basic-troop",
            contestants);
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryCreate(seed, out var snapshot));
        return snapshot;
    }

    private static TournamentSessionSnapshot CreateStartedSession(
        TournamentSessionRegistry registry,
        bool humanInMatch)
    {
        TournamentSessionSnapshot snapshot = CreateSession(registry, "session-1", "town-1");
        registry.TryJoin(
            snapshot.SessionId,
            snapshot.Revision,
            "player-1",
            "player-character-1",
            "Player One",
            500,
            false,
            out snapshot);
        registry.TryJoin(
            snapshot.SessionId,
            snapshot.Revision,
            "player-2",
            "player-character-2",
            "Player Two",
            501,
            false,
            out snapshot);

        string firstSlot = humanInMatch
            ? snapshot.Contestants.Single(contestant => contestant.ControllerId == "player-1").SlotId
            : "session-1:slot:0";
        string secondSlot = humanInMatch
            ? "session-1:slot:0"
            : "session-1:slot:1";
        var teams = new[]
        {
            new TournamentTeamData("team-1", new[] { firstSlot }, 0, false, 1, null),
            new TournamentTeamData("team-2", new[] { secondSlot }, 0, false, 2, null)
        };
        var match = new TournamentMatchData(
            "match-1",
            "round-1",
            0,
            1,
            1,
            teams,
            Array.Empty<string>(),
            1);
        var rounds = new[] { new TournamentRoundData("round-1", 0, 0, new[] { match }) };

        Assert.Equal(TournamentMutationStatus.Applied, registry.TryStart(
            snapshot.SessionId,
            snapshot.Revision,
            "player-1",
            rounds,
            match.MatchId,
            out snapshot));
        return snapshot;
    }

    private static TournamentSessionSnapshot CreateLiveSession(TournamentSessionRegistry registry)
    {
        TournamentSessionSnapshot snapshot = CreateStartedSession(registry, humanInMatch: true);
        registry.TryEnterMission(snapshot.SessionId, snapshot.Revision, "player-1", out snapshot);
        registry.TryEnterMission(snapshot.SessionId, snapshot.Revision, "player-2", out snapshot);
        registry.TryChoose(
            snapshot.SessionId, snapshot.Revision, snapshot.CurrentMatchId, "player-1",
            TournamentPlayerChoice.Join, out snapshot, out _);
        registry.TryChoose(
            snapshot.SessionId, snapshot.Revision, snapshot.CurrentMatchId, "player-2",
            TournamentPlayerChoice.Watch, out snapshot, out _);
        Assert.Equal(TournamentSessionPhase.LiveMatch, snapshot.Phase);
        return snapshot;
    }

    private static TournamentSpawnManifestData CreateManifest(
        TournamentSessionSnapshot snapshot,
        long revision,
        long bracketRevision)
    {
        TournamentMatchData match = snapshot.Rounds.SelectMany(round => round.Matches)
            .Single(candidate => candidate.MatchId == snapshot.CurrentMatchId);
        TournamentAgentSpawnData[] agents = match.Teams
            .SelectMany(team => team.ParticipantSlotIds.Select(slotId => (team, slotId)))
            .Select(entry =>
            {
                TournamentContestantData contestant = snapshot.Contestants
                    .Single(candidate => candidate.SlotId == entry.slotId);
                string owner = contestant.IsHuman && !contestant.IsReplaced
                    ? contestant.ControllerId
                    : snapshot.HostControllerId;
                return new TournamentAgentSpawnData(
                    Guid.NewGuid(), contestant.SlotId, contestant.CharacterId, contestant.DescriptorSeed,
                    entry.team.TeamId, entry.team.TeamColor, entry.team.BannerCode, owner,
                    new[] { new EquipmentElement(new ItemObject("weapon")) },
                    Vec3.Zero, new Vec2(0, 1), 100,
                    Guid.Empty, null, 0, Array.Empty<EquipmentElement>(), 0);
            })
            .ToArray();
        return new TournamentSpawnManifestData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            revision,
            bracketRevision,
            1,
            agents);
    }
}
