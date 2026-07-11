using Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using GameInterface.Services.Tournaments.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Tournaments;

public class TournamentCanonicalClientFlowTests : SyncTestBase
{
    public TournamentCanonicalClientFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void CanonicalSnapshot_LateSpectatorSequencedSettlementAndTombstone_StayIdentical()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        SetControllerId(clients[0], "player-a");
        SetControllerId(clients[1], "spectator-b");
        TournamentSessionSnapshot initial = CreateSnapshot(1, Array.Empty<string>(), 1);

        Broadcast(new NetworkTournamentSessionSnapshot(initial));

        AssertCanonicalState(clients[0], 1, 1, Array.Empty<string>());
        AssertCanonicalState(clients[1], 1, 1, Array.Empty<string>());

        TournamentSessionSnapshot lateSpectator = CreateSnapshot(2, new[] { "spectator-b" }, 2);
        Broadcast(new NetworkTournamentSessionSnapshot(lateSpectator));

        AssertCanonicalState(clients[0], 2, 2, new[] { "spectator-b" });
        AssertCanonicalState(clients[1], 2, 2, new[] { "spectator-b" });

        var summaries = new List<CoopTournamentVM.BetSummary>();
        long acceptedSequence = 0;
        clients[0].Call(() =>
        {
            var controller = clients[0].Resolve<TournamentUIController>();
            controller.BetResultReceived += result =>
            {
                Assert.True(controller.TryGetSession(initial.SessionId, out var snapshot));
                if (!CoopTournamentVM.TryGetAcceptedBetSummary(
                        snapshot,
                        result,
                        acceptedSequence,
                        out var summary))
                {
                    return;
                }

                acceptedSequence = result.Sequence;
                summaries.Add(summary);
            };
        });

        Send(clients[0], CreateBetResult(1, false, 100, 60, 180));
        Send(clients[0], CreateBetResult(1, false, 150, 100, 260));
        Send(clients[0], CreateBetResult(2, true, 0, 0, 0));

        Assert.Equal(2, summaries.Count);
        Assert.Equal(100, summaries[0].BettedDenars);
        Assert.Equal(60, summaries[0].ThisRoundBettedDenars);
        Assert.True(summaries[1].IsSettlement);
        Assert.Equal(0, summaries[1].BettedDenars);
        Assert.Equal(0, summaries[1].ThisRoundBettedDenars);
        Assert.Equal(0, clients[1].InternalMessages.GetMessageCount<NetworkTournamentBetResult>());

        int removalCount = 0;
        foreach (EnvironmentInstance client in clients)
        {
            client.Call(() => client.Resolve<TournamentUIController>().SessionRemoved +=
                _ => Interlocked.Increment(ref removalCount));
        }

        Broadcast(new NetworkTournamentSessionRemoved(initial.SessionId, initial.TownId));

        AssertRemoved(clients[0], initial.SessionId);
        AssertRemoved(clients[1], initial.SessionId);
        Assert.Equal(2, Volatile.Read(ref removalCount));
    }

    private void Broadcast<T>(T message) where T : Common.Messaging.IMessage
        => Server.Call(() => Server.Resolve<INetwork>().SendAll(message));

    private void Send<T>(EnvironmentInstance client, T message) where T : Common.Messaging.IMessage
        => Server.Call(() => Server.Resolve<INetwork>().Send(client.NetPeer, message));

    private static void SetControllerId(EnvironmentInstance client, string controllerId)
        => client.Call(() => client.Resolve<IControllerIdProvider>().SetControllerId(controllerId));

    private static void AssertCanonicalState(
        EnvironmentInstance client,
        long revision,
        int voterCount,
        string[] spectators)
    {
        client.Call(() =>
        {
            var registry = client.Resolve<ITournamentSessionRegistry>();
            Assert.True(registry.TryGet("session-a", out var registrySnapshot));
            Assert.Equal(revision, registrySnapshot.Revision);
            Assert.Equal(voterCount, registrySnapshot.VoterCount);
            Assert.Equal(spectators, registrySnapshot.SpectatorControllerIds);
            Assert.Equal("slot-a", registrySnapshot.Rounds[0].Matches[0].Teams[0].ParticipantSlotIds[0]);

            var controller = client.Resolve<TournamentUIController>();
            Assert.True(controller.TryGetSession("session-a", out var uiSnapshot));
            Assert.Equal(registrySnapshot.Revision, uiSnapshot.Revision);
            Assert.Equal(registrySnapshot.BracketRevision, uiSnapshot.BracketRevision);
            Assert.Equal(registrySnapshot.CurrentMatchId, uiSnapshot.CurrentMatchId);
            Assert.Equal(registrySnapshot.VoterCount, uiSnapshot.VoterCount);
        });
    }

    private static void AssertRemoved(EnvironmentInstance client, string sessionId)
    {
        client.Call(() =>
        {
            Assert.False(client.Resolve<ITournamentSessionRegistry>().TryGet(sessionId, out _));
            Assert.False(client.Resolve<TournamentUIController>().TryGetSession(sessionId, out _));
        });
    }

    private static NetworkTournamentBetResult CreateBetResult(
        long sequence,
        bool isSettlement,
        int cumulativeBettedDenars,
        int thisRoundBettedDenars,
        int expectedPayout)
        => new(
            "session-a",
            2,
            sequence,
            "match-a",
            true,
            isSettlement ? "Tournament bet settled" : null,
            cumulativeBettedDenars,
            thisRoundBettedDenars,
            expectedPayout,
            isSettlement);

    private static TournamentSessionSnapshot CreateSnapshot(
        long revision,
        string[] spectators,
        int voterCount)
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
                    new[] { contestant.SlotId },
                    0,
                    false,
                    0,
                    null)
            },
            Array.Empty<string>());

        return new TournamentSessionSnapshot(
            "session-a",
            "mission-a",
            "town-a",
            "arena-a",
            "prize-a",
            TournamentSessionPhase.AwaitingChoices,
            revision,
            1,
            match.MatchId,
            "player-a",
            Array.Empty<string>(),
            new[] { contestant },
            spectators,
            Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData("round-a", 0, 0, new[] { match }) },
            0,
            0,
            voterCount,
            true,
            false,
            null);
    }
}
