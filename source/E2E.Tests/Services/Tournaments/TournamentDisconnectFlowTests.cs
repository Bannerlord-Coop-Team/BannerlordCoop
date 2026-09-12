using Common.Network.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Tournaments;

public class TournamentDisconnectFlowTests : SyncTestBase
{
    private const string SessionId = "disconnect-session";
    private const string HostControllerId = "tournament-host";
    private const string SuccessorControllerId = "tournament-successor";

    public TournamentDisconnectFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ConfirmedHostDisconnect_PromotesConfirmedSuccessor()
    {
        EnvironmentInstance host = Clients.First();
        EnvironmentInstance successor = Clients.Skip(1).First();
        string townId = TestEnvironment.CreateRegisteredObject<Town>();
        string settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        string townPartyId = TestEnvironment.CreateRegisteredObject<PartyBase>();
        string cultureId = TestEnvironment.CreateRegisteredObject<CultureObject>();
        string troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(townPartyId, out var townParty));
            Assert.True(Server.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            culture.BasicTroop = troop;
            settlement.Culture = culture;
            settlement.Town = town;
            townParty.Settlement = settlement;
            town._owner = townParty;

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                HostControllerId,
                string.Empty,
                string.Empty,
                string.Empty,
                "host-character")));
            Assert.True(playerManager.AddPlayer(new Player(
                SuccessorControllerId,
                string.Empty,
                string.Empty,
                string.Empty,
                "successor-character")));
            playerManager.SetPeer(HostControllerId, host.NetPeer);
            playerManager.SetPeer(SuccessorControllerId, successor.NetPeer);

            Assert.True(Server.Resolve<ITournamentSessionRegistry>().ApplySnapshot(CreateSnapshot(townId)));
        });

        Server.SimulateMessage(host.NetPeer, new NetworkTournamentMissionEntered(SessionId, 1));
        Server.SimulateMessage(successor.NetPeer, new NetworkTournamentMissionEntered(SessionId, 1));

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<ITournamentSessionRegistry>().TryGet(SessionId, out var entered));
            Assert.Equal(HostControllerId, entered.HostControllerId);
            Assert.Equal(new[] { SuccessorControllerId }, entered.SuccessorControllerIds);
        });

        Server.SimulateMessage(this, new PlayerDisconnected(host.NetPeer, default));

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<ITournamentSessionRegistry>().TryGet(SessionId, out var disconnected));
            Assert.Equal(SuccessorControllerId, disconnected.HostControllerId);
            Assert.Empty(disconnected.SuccessorControllerIds);
            Assert.DoesNotContain(disconnected.Contestants, contestant =>
                contestant.IsHuman && contestant.ControllerId == HostControllerId);
        });
    }

    private static TournamentSessionSnapshot CreateSnapshot(string townId)
    {
        TournamentContestantData[] contestants =
        {
            CreateContestant("host-slot", "host-character", HostControllerId, "Tournament Host"),
            CreateContestant("successor-slot", "successor-character", SuccessorControllerId, "Tournament Successor")
        };
        var match = new TournamentMatchData(
            "match-1",
            "round-1",
            0,
            2,
            1,
            new[]
            {
                new TournamentTeamData(
                    "team-1",
                    contestants.Select(contestant => contestant.SlotId).ToArray(),
                    0,
                    false,
                    0,
                    null)
            },
            Array.Empty<string>());

        return new TournamentSessionSnapshot(
            SessionId,
            "disconnect-mission",
            townId,
            "arena",
            "prize",
            TournamentSessionPhase.AwaitingChoices,
            1,
            1,
            match.MatchId,
            null,
            Array.Empty<string>(),
            contestants,
            Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData("round-1", 0, 0, new[] { match }) },
            0,
            0,
            2,
            true,
            false,
            null);
    }

    private static TournamentContestantData CreateContestant(
        string slotId,
        string characterId,
        string controllerId,
        string displayName)
        => new(
            slotId,
            characterId,
            1,
            controllerId,
            displayName,
            true,
            false,
            true,
            characterId);
}
