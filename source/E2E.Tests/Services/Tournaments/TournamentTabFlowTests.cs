using Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using GameInterface.Services.Tournaments.UI;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Tournaments;

public class TournamentTabFlowTests : SyncTestBase
{
    private const string SessionId = "session-a";
    private const string TownId = "town-a";

    public TournamentTabFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void AdvanceChoice_ContestantInMatch_Joins_OthersWatch()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        SetControllerId(clients[0], "player-a");
        SetControllerId(clients[1], "spectator-b");
        TournamentSessionSnapshot snapshot = CreateSnapshot(
            TournamentSessionPhase.AwaitingChoices,
            1,
            humanInCurrentMatch: true,
            skipAllowed: false);

        Broadcast(new NetworkTournamentSessionSnapshot(snapshot));

        AssertAdvanceChoice(clients[0], "player-a", TournamentPlayerChoice.Join);
        AssertAdvanceChoice(clients[1], "spectator-b", TournamentPlayerChoice.Watch);
    }

    [Fact]
    public void AdvanceChoice_NoHumanInMatch_Skips()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        SetControllerId(clients[0], "player-a");
        SetControllerId(clients[1], "spectator-b");
        TournamentSessionSnapshot snapshot = CreateSnapshot(
            TournamentSessionPhase.AwaitingChoices,
            1,
            humanInCurrentMatch: false,
            skipAllowed: true);

        Broadcast(new NetworkTournamentSessionSnapshot(snapshot));

        AssertAdvanceChoice(clients[0], "player-a", TournamentPlayerChoice.Skip);
        AssertAdvanceChoice(clients[1], "spectator-b", TournamentPlayerChoice.Skip);
    }

    [Fact]
    public void LeaveMenuPredicate_LiveMatchSpectatorOnly()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        SetControllerId(clients[0], "player-a");
        SetControllerId(clients[1], "spectator-b");
        TournamentSessionSnapshot snapshot = CreateSnapshot(
            TournamentSessionPhase.LiveMatch,
            1,
            humanInCurrentMatch: true,
            skipAllowed: false);

        Broadcast(new NetworkTournamentSessionSnapshot(snapshot));

        AssertLeaveMenu(clients[0], "player-a", expectedOpen: false);
        AssertLeaveMenu(clients[1], "spectator-b", expectedOpen: true);
    }

    [Fact]
    public void TryLeaveActive_SpectatorDuringLiveMatch_DropsFromSession()
    {
        Server.Call(() =>
        {
            var registry = Server.Resolve<ITournamentSessionRegistry>();
            TournamentSessionSnapshot snapshot = CreateStartedLiveSession(registry);

            Assert.Contains("spectator-b", snapshot.SpectatorControllerIds);

            TournamentMutationStatus status = registry.TryLeaveActive(
                snapshot.SessionId,
                snapshot.Revision,
                "spectator-b",
                900,
                "Tournament Recruit",
                out snapshot,
                out var outcome,
                out var noViewers);

            Assert.Equal(TournamentMutationStatus.Applied, status);
            Assert.Equal(TournamentBallotOutcome.Open, outcome);
            Assert.DoesNotContain("spectator-b", snapshot.SpectatorControllerIds);
            Assert.Equal(TournamentSessionPhase.LiveMatch, snapshot.Phase);
            Assert.False(noViewers);
            Assert.Equal(2, snapshot.Contestants.Count(contestant => contestant.IsHuman));
            Assert.DoesNotContain(snapshot.Contestants, contestant => contestant.IsReplaced);
        });
    }

    private static void AssertAdvanceChoice(
        EnvironmentInstance client,
        string controllerId,
        TournamentPlayerChoice expected)
    {
        client.Call(() =>
        {
            Assert.True(client.Resolve<ITournamentSessionRegistry>().TryGet(SessionId, out var snapshot));
            CoopTournamentVM.UIState state = CoopTournamentVM.CalculateUIState(snapshot, controllerId, false);
            Assert.Equal(expected, CoopTournamentVM.GetAdvanceChoice(state));
            Assert.False(CoopTournamentVM.ShouldOpenLeaveMenu(state));
        });
    }

    private static void AssertLeaveMenu(
        EnvironmentInstance client,
        string controllerId,
        bool expectedOpen)
    {
        client.Call(() =>
        {
            Assert.True(client.Resolve<ITournamentSessionRegistry>().TryGet(SessionId, out var snapshot));
            CoopTournamentVM.UIState state = CoopTournamentVM.CalculateUIState(snapshot, controllerId, false);
            Assert.Null(CoopTournamentVM.GetAdvanceChoice(state));
            Assert.Equal(expectedOpen, CoopTournamentVM.ShouldOpenLeaveMenu(state));
        });
    }

    private void Broadcast<T>(T message) where T : Common.Messaging.IMessage
        => Server.Call(() => Server.Resolve<INetwork>().SendAll(message));

    private static void SetControllerId(EnvironmentInstance client, string controllerId)
        => client.Call(() => client.Resolve<IControllerIdProvider>().SetControllerId(controllerId));

    private static TournamentSessionSnapshot CreateSnapshot(
        TournamentSessionPhase phase,
        long revision,
        bool humanInCurrentMatch,
        bool skipAllowed)
    {
        var contestant = new TournamentContestantData(
            "slot-a",
            "character-a",
            17,
            "player-a",
            "Player A",
            true,
            false,
            true,
            "npc-a");
        string currentMatchSlot = humanInCurrentMatch ? contestant.SlotId : "npc-slot";
        var match = new TournamentMatchData(
            "match-a",
            "round-a",
            0,
            1,
            1,
            new[]
            {
                new TournamentTeamData(
                    "team-a",
                    new[] { currentMatchSlot },
                    0,
                    false,
                    0,
                    null)
            },
            Array.Empty<string>());

        return new TournamentSessionSnapshot(
            SessionId,
            "mission-a",
            TownId,
            "arena-a",
            "prize-a",
            phase,
            revision,
            1,
            match.MatchId,
            "player-a",
            Array.Empty<string>(),
            new[] { contestant },
            new[] { "spectator-b" },
            Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData("round-a", 0, 0, new[] { match }) },
            0,
            0,
            2,
            skipAllowed,
            false,
            null);
    }

    private static TournamentSessionSnapshot CreateStartedLiveSession(ITournamentSessionRegistry registry)
    {
        TournamentContestantData[] contestants = Enumerable.Range(0, 16)
            .Select(index => new TournamentContestantData(
                $"tab-session:slot:{index}",
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
            "tab-session",
            "tab-session",
            TownId,
            "arena-scene",
            "prize-item",
            "basic-troop",
            contestants);
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryCreate(seed, out var snapshot));
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryJoin(
            snapshot.SessionId, snapshot.Revision, "player-1", "player-character-1", "Player One", 500, false, out snapshot));
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryJoin(
            snapshot.SessionId, snapshot.Revision, "player-2", "player-character-2", "Player Two", 501, false, out snapshot));

        string firstSlot = snapshot.Contestants.Single(contestant => contestant.ControllerId == "player-1").SlotId;
        var teams = new[]
        {
            new TournamentTeamData("team-1", new[] { firstSlot }, 0, false, 1, null),
            new TournamentTeamData("team-2", new[] { "tab-session:slot:0" }, 0, false, 2, null)
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
            snapshot.SessionId, snapshot.Revision, "player-1", rounds, match.MatchId, out snapshot));
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryRequestSpectate(
            snapshot.SessionId, snapshot.Revision, "spectator-b", out snapshot));
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryEnterMission(
            snapshot.SessionId, snapshot.Revision, "player-1", out snapshot));
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryEnterMission(
            snapshot.SessionId, snapshot.Revision, "player-2", out snapshot));
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryChoose(
            snapshot.SessionId, snapshot.Revision, snapshot.CurrentMatchId, "player-1",
            TournamentPlayerChoice.Join, out snapshot, out _));
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryChoose(
            snapshot.SessionId, snapshot.Revision, snapshot.CurrentMatchId, "player-2",
            TournamentPlayerChoice.Watch, out snapshot, out _));
        Assert.Equal(TournamentMutationStatus.Applied, registry.TryChoose(
            snapshot.SessionId, snapshot.Revision, snapshot.CurrentMatchId, "spectator-b",
            TournamentPlayerChoice.Watch, out snapshot, out _));

        Assert.Equal(TournamentSessionPhase.LiveMatch, snapshot.Phase);
        return snapshot;
    }
}