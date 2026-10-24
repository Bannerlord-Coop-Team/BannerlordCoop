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
    public void SnapshotBroadcast_DrivesAdvanceChoiceThenLiveMatchLeaveMenu()
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

        Broadcast(new NetworkTournamentSessionSnapshot(CreateSnapshot(
            TournamentSessionPhase.LiveMatch,
            2,
            humanInCurrentMatch: true,
            skipAllowed: false)));

        AssertLeaveMenu(clients[0], "player-a", expectedOpen: false);
        AssertLeaveMenu(clients[1], "spectator-b", expectedOpen: true);
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
}