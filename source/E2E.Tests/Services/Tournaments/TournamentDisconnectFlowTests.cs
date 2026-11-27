using Common.Network;
using Common.Network.Messages;
using E2E.Tests.Environment;
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
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Tournaments;

public class TournamentDisconnectFlowTests : SyncTestBase
{
    private const string SessionId = "disconnect-session";
    private const string HostControllerId = "tournament-host";
    private const string OtherControllerId = "tournament-other";
    private const string HeroSlotId = "host-slot";
    private const string OtherSlotId = "other-slot";

    public TournamentDisconnectFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ReturningHost_WithSuccessor_RejoinsAsSuccessorAndCanFight()
    {
        EnvironmentInstance host = Clients.First();
        EnvironmentInstance other = Clients.Skip(1).First();
        var (townId, heroCharacterId) = SetUpTournament(host, other, otherIsSpectator: false);

        Disconnect(host);
        var reserved = GetSnapshot();
        Assert.Equal(TournamentSessionPhase.AwaitingChoices, reserved.Phase);
        Assert.Equal(OtherControllerId, reserved.HostControllerId);
        Assert.Equal(heroCharacterId, FindHostSlot(reserved).CharacterId);
        Assert.Empty(reserved.SuccessorControllerIds);
        Assert.Equal(1, reserved.VoterCount);

        Reconnect(host, host);
        var resumed = ResumeTournament(host, townId, reserved.Revision, heroCharacterId);
        Assert.Equal(OtherControllerId, resumed.HostControllerId);
        Assert.Equal(new[] { HostControllerId }, resumed.SuccessorControllerIds);
        Send(host, new NetworkRequestTournamentChoice(
            SessionId, resumed.Revision, resumed.CurrentMatchId, TournamentPlayerChoice.Join));
        Send(other, new NetworkRequestTournamentChoice(
            SessionId, resumed.Revision, resumed.CurrentMatchId, TournamentPlayerChoice.Join));

        AssertOriginalHeroCanFight(heroCharacterId);
    }

    [Fact]
    public void ReturningHost_Alone_ReclaimsHostAndCanFight()
    {
        EnvironmentInstance host = Clients.First();
        EnvironmentInstance returning = Clients.Skip(1).First();
        var (townId, heroCharacterId) = SetUpTournament(host, null, otherIsSpectator: false);

        Disconnect(host);
        var reserved = GetSnapshot();
        Assert.Equal(TournamentSessionPhase.AwaitingChoices, reserved.Phase);
        Assert.Null(reserved.HostControllerId);
        Assert.Equal(heroCharacterId, FindHostSlot(reserved).CharacterId);
        Assert.Empty(reserved.SuccessorControllerIds);
        Assert.Equal(0, reserved.VoterCount);

        // Use the fresh client instance to model restarting the game with the same controller.
        Reconnect(host, returning);
        var resumed = ResumeTournament(returning, townId, reserved.Revision, heroCharacterId);
        Assert.Equal(HostControllerId, resumed.HostControllerId);
        Assert.Empty(resumed.SuccessorControllerIds);
        Send(returning, new NetworkRequestTournamentChoice(
            SessionId, resumed.Revision, resumed.CurrentMatchId, TournamentPlayerChoice.Join));

        AssertOriginalHeroCanFight(heroCharacterId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParkedTournament_ResolvesWhenReservedEntrantLeavesOrIsRemoved(bool removeRegistration)
    {
        EnvironmentInstance host = Clients.First();
        SetUpTournament(host, null, otherIsSpectator: false);
        Disconnect(host);
        var parked = GetSnapshot();
        Assert.Equal(TournamentSessionPhase.AwaitingChoices, parked.Phase);
        Assert.True(FindHostSlot(parked).IsDisconnected);

        if (removeRegistration)
        {
            Server.Call(() =>
            {
                var players = Server.Resolve<IPlayerManager>();
                Assert.True(players.TryGetPlayer(HostControllerId, out var player));
                Assert.True(players.RemovePlayer(player));
            });
            Server.PumpGameThread();
        }
        else
        {
            Reconnect(host, host);
            Send(host, new NetworkRequestLeaveActiveTournament(SessionId, parked.Revision));
        }

        Server.Call(() =>
        {
            var registry = Server.Resolve<ITournamentSessionRegistry>();
            if (registry.TryGet(SessionId, out var snapshot))
            {
                Assert.True(snapshot.IsCompleted);
                Assert.True(FindHostSlot(snapshot).IsReplaced);
            }
        });
    }

    private TournamentSessionSnapshot ResumeTournament(
        EnvironmentInstance returning, string townId, long revision, string heroCharacterId)
    {
        Send(returning, new NetworkRequestJoinTournament(townId, SessionId, revision));
        var entry = Assert.Single(returning.InternalMessages.GetMessages<NetworkEnterTournamentMission>());
        Assert.False(entry.IsSpectator);
        Assert.Equal(heroCharacterId, FindHostSlot(entry.Snapshot).CharacterId);
        Send(returning, new NetworkTournamentMissionEntered(SessionId, entry.Snapshot.Revision));
        return GetSnapshot();
    }

    private void AssertOriginalHeroCanFight(string heroCharacterId)
    {
        var live = GetSnapshot();
        Assert.Equal(TournamentSessionPhase.LiveMatch, live.Phase);
        Assert.Contains(live.Choices, choice =>
            choice.ControllerId == HostControllerId && choice.Choice == TournamentPlayerChoice.Join);
        Assert.Contains(live.Rounds[0].Matches[0].Teams, team => team.ParticipantSlotIds.Contains(HeroSlotId));
        Assert.Equal(heroCharacterId, FindHostSlot(live).CharacterId);
    }

    [Fact]
    public void MatchSkippedWhileHeroIsAway_RecordsLossAndNotifiesOnReconnect()
    {
        EnvironmentInstance host = Clients.First();
        EnvironmentInstance other = Clients.Skip(1).First();
        var (townId, heroCharacterId) = SetUpTournament(host, other, otherIsSpectator: true);

        Disconnect(host);
        var reserved = GetSnapshot();
        Send(other, new NetworkRequestTournamentChoice(
            SessionId, reserved.Revision, reserved.CurrentMatchId, TournamentPlayerChoice.Skip));

        var completed = GetSnapshot();
        Assert.True(completed.IsCompleted);
        Assert.Equal(OtherSlotId, completed.WinnerSlotId);
        Assert.Equal(heroCharacterId, FindHostSlot(completed).CharacterId);
        var match = Assert.Single(Assert.Single(completed.Rounds).Matches);
        Assert.Equal(new[] { OtherSlotId }, match.WinnerSlotIds);
        Assert.Contains(match.Teams, team => team.ParticipantSlotIds.Contains(HeroSlotId));

        Reconnect(host, host);
        Send(host, new NetworkRequestTournamentState());
        Send(host, new NetworkRequestTournamentState());

        var notification = Assert.Single(host.InternalMessages.GetMessages<NetworkTournamentDisconnectLoss>());
        Assert.Equal(townId, notification.TownId);
        Assert.Empty(other.InternalMessages.GetMessages<NetworkTournamentDisconnectLoss>());
    }

    [Fact]
    public void ReconnectedEntrant_ReceivesLossWhenSpectatorSkipsBeforeTheyResume()
    {
        EnvironmentInstance host = Clients.First();
        EnvironmentInstance spectator = Clients.Skip(1).First();
        var (townId, _) = SetUpTournament(host, spectator, otherIsSpectator: true);

        Disconnect(host);
        Reconnect(host, host);
        Send(host, new NetworkRequestTournamentState());
        Assert.Empty(host.InternalMessages.GetMessages<NetworkTournamentDisconnectLoss>());
        var reserved = GetSnapshot();
        Assert.True(FindHostSlot(reserved).IsDisconnected);

        Send(spectator, new NetworkRequestTournamentChoice(
            SessionId, reserved.Revision, reserved.CurrentMatchId, TournamentPlayerChoice.Skip));
        var notification = Assert.Single(host.InternalMessages.GetMessages<NetworkTournamentDisconnectLoss>());
        Assert.Equal(townId, notification.TownId);

        Send(host, new NetworkRequestTournamentState());
        Assert.Single(host.InternalMessages.GetMessages<NetworkTournamentDisconnectLoss>());
        Assert.Empty(spectator.InternalMessages.GetMessages<NetworkTournamentDisconnectLoss>());
    }

    [Fact]
    public void DisconnectDuringLiveMatch_ReplicatesForfeitAndNotifiesOnReconnect()
    {
        EnvironmentInstance host = Clients.First();
        EnvironmentInstance other = Clients.Skip(1).First();
        var (townId, heroCharacterId) = SetUpTournament(host, other, otherIsSpectator: false);

        var pending = GetSnapshot();
        var join = new NetworkRequestTournamentChoice(
            SessionId, pending.Revision, pending.CurrentMatchId, TournamentPlayerChoice.Join);
        Send(host, join);
        Send(other, join);
        Assert.Equal(TournamentSessionPhase.LiveMatch, GetSnapshot().Phase);

        Disconnect(host);

        var completed = Assert.Single(other.InternalMessages.GetMessages<NetworkTournamentSessionSnapshot>(),
            message => message.Snapshot.IsCompleted).Snapshot;
        Assert.Equal(OtherSlotId, completed.WinnerSlotId);
        var disconnected = FindHostSlot(completed);
        Assert.Equal(heroCharacterId, disconnected.CharacterId);
        Assert.True(disconnected.IsDisconnected);
        var match = Assert.Single(Assert.Single(completed.Rounds).Matches);
        Assert.Equal(new[] { OtherSlotId }, match.WinnerSlotIds);
        Assert.Contains(match.Teams, team => team.ParticipantSlotIds.Contains(HeroSlotId));

        Reconnect(host, host);
        Send(host, new NetworkRequestTournamentState());
        Send(host, new NetworkRequestTournamentState());
        var notice = Assert.Single(host.InternalMessages.GetMessages<NetworkTournamentDisconnectLoss>());
        Assert.Equal(townId, notice.TownId);
    }

    private (string TownId, string CharacterId) SetUpTournament(
        EnvironmentInstance host, EnvironmentInstance? other, bool otherIsSpectator)
    {
        string townId = TestEnvironment.CreateRegisteredObject<Town>();
        string settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        string townPartyId = TestEnvironment.CreateRegisteredObject<PartyBase>();
        string cultureId = TestEnvironment.CreateRegisteredObject<CultureObject>();
        string troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string hostCharacterId = RegisterPlayer(HostControllerId, settlementId);
        bool otherCompetes = other != null && !otherIsSpectator;
        string otherCharacterId = troopId;
        if (other != null)
            otherCharacterId = RegisterPlayer(OtherControllerId, settlementId);

        Server.Call(() =>
        {
            BannerManager.Initialize();
            var town = Server.GetRegisteredObject<Town>(townId);
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            var townParty = Server.GetRegisteredObject<PartyBase>(townPartyId);
            var culture = Server.GetRegisteredObject<CultureObject>(cultureId);
            culture.BasicTroop = Server.GetRegisteredObject<CharacterObject>(troopId);
            settlement.Culture = culture;
            settlement.Town = town;
            townParty.Settlement = settlement;
            town._owner = townParty;

            var contestants = new[]
            {
                CreateContestant(HeroSlotId, hostCharacterId, 1, HostControllerId),
                CreateContestant(OtherSlotId, otherCompetes ? otherCharacterId : troopId, 2,
                    otherCompetes ? OtherControllerId : null)
            };
            var seed = new TournamentSessionSeed(SessionId, "disconnect-mission", townId, "arena",
                null, troopId, contestants);
            var registry = Server.Resolve<ITournamentSessionRegistry>();
            Assert.Equal(TournamentMutationStatus.Applied, registry.TryCreate(seed, out var preparing));
            var match = new TournamentMatchData(
                "match-1", "round-1", 0, 1, 1,
                contestants.Select((contestant, index) => new TournamentTeamData(
                    $"team-{index}", new[] { contestant.SlotId }, 0, false, 0, null)).ToArray(),
                Array.Empty<string>(),
                (int)TournamentGame.QualificationMode.TeamScore);
            Assert.Equal(TournamentMutationStatus.Applied, registry.TryStart(
                SessionId, preparing.Revision, HostControllerId,
                new[] { new TournamentRoundData("round-1", 0, 0, new[] { match }) },
                match.MatchId, out _));
        });

        TestEnvironment.ConnectRegisteredPlayer(host, HostControllerId);
        Send(host, new NetworkTournamentMissionEntered(SessionId, GetSnapshot().Revision));
        if (other != null)
        {
            TestEnvironment.ConnectRegisteredPlayer(other, OtherControllerId);
            if (otherIsSpectator)
                Send(other, new NetworkRequestSpectateTournament(SessionId, GetSnapshot().Revision));

            Send(other, new NetworkTournamentMissionEntered(SessionId, GetSnapshot().Revision));
            other.InternalMessages.Clear();
        }

        var entered = GetSnapshot();
        Assert.Equal(HostControllerId, entered.HostControllerId);
        Assert.Equal(other != null ? new[] { OtherControllerId } : Array.Empty<string>(), entered.SuccessorControllerIds);
        host.InternalMessages.Clear();
        return (townId, hostCharacterId);
    }

    private string RegisterPlayer(string controller, string settlementId)
    {
        string heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            var hero = Server.GetRegisteredObject<Hero>(heroId);
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            hero.PartyBelongedTo = party;
            party.CurrentSettlement = Server.GetRegisteredObject<Settlement>(settlementId);
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                new Player(controller, heroId, partyId, string.Empty, characterId)));
        });
        return characterId;
    }

    private void Disconnect(EnvironmentInstance client)
    {
        Server.Resolve<TestNetworkRouter>().Disconnect(client.NetPeer);
        Server.Call(() => Server.Resolve<IPlayerManager>().ClearPeer(client.NetPeer));
        Server.SimulateMessage(this, new PlayerDisconnected(client.NetPeer, default));
        Server.PumpGameThread();
    }

    private void Reconnect(EnvironmentInstance oldClient, EnvironmentInstance newClient)
    {
        if (oldClient == newClient)
            Server.Resolve<TestNetworkRouter>().Reconnect(newClient.NetPeer);
        TestEnvironment.ConnectRegisteredPlayer(newClient, HostControllerId);
    }

    private static void Send<T>(EnvironmentInstance client, T message) where T : Common.Messaging.IMessage
        => client.Call(() => client.Resolve<INetwork>().SendAll(message));

    private TournamentSessionSnapshot GetSnapshot()
    {
        TournamentSessionSnapshot snapshot = null!;
        Server.Call(() => Assert.True(Server.Resolve<ITournamentSessionRegistry>().TryGet(SessionId, out snapshot)));
        return snapshot;
    }

    private static TournamentContestantData FindHostSlot(TournamentSessionSnapshot snapshot)
        => Assert.Single(snapshot.Contestants, contestant => contestant.SlotId == HeroSlotId);

    private static TournamentContestantData CreateContestant(
        string slotId, string characterId, int seed, string? controllerId)
        => new(slotId, characterId, seed, controllerId, slotId, controllerId != null, false, false, null);
}
