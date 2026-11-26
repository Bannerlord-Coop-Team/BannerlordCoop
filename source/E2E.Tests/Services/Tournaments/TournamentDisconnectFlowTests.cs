using Common.Network;
using Common.Network.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Handlers;
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

#if DEBUG
    [Fact]
    public void DebugResolveMatch_AdvancesSelectedEntrantsTeam()
    {
        EnvironmentInstance host = Clients.First();
        EnvironmentInstance other = Clients.Skip(1).First();
        var (townId, _) = SetUpTournament(host, other, otherIsSpectator: false);

        Server.Call(() => Assert.True(Server.Resolve<TournamentSessionHandler>()
            .ResolveDebugMatch(townId, OtherControllerId).Succeeded));

        var completed = GetSnapshot();
        Assert.True(completed.IsCompleted);
        Assert.Equal(OtherSlotId, completed.WinnerSlotId);
    }
#endif

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReturningHost_KeepsHeroAndCanJoinNextMatch(bool withSuccessor)
    {
        EnvironmentInstance host = Clients.First();
        EnvironmentInstance other = Clients.Skip(1).First();
        var (townId, heroCharacterId) = SetUpTournament(
            host, withSuccessor ? other : null, otherIsSpectator: false);

        Disconnect(host);
        var reserved = GetSnapshot();
        Assert.Equal(TournamentSessionPhase.AwaitingChoices, reserved.Phase);
        Assert.Equal(withSuccessor ? OtherControllerId : null, reserved.HostControllerId);
        Assert.Equal(heroCharacterId, FindHostSlot(reserved).CharacterId);
        Assert.Empty(reserved.SuccessorControllerIds);
        Assert.Equal(withSuccessor ? 1 : 0, reserved.VoterCount);

        EnvironmentInstance returning = withSuccessor ? host : other;
        Reconnect(host, returning);
        Send(returning, new NetworkRequestJoinTournament(townId, SessionId, reserved.Revision));
        var entry = Assert.Single(returning.InternalMessages.GetMessages<NetworkEnterTournamentMission>());

        Assert.False(entry.IsSpectator);
        Assert.Equal(heroCharacterId, FindHostSlot(entry.Snapshot).CharacterId);

        Send(returning, new NetworkTournamentMissionEntered(SessionId, entry.Snapshot.Revision));
        var resumed = GetSnapshot();
        Assert.Equal(withSuccessor ? OtherControllerId : HostControllerId, resumed.HostControllerId);
        Assert.Equal(withSuccessor ? new[] { HostControllerId } : Array.Empty<string>(), resumed.SuccessorControllerIds);
        Send(returning, new NetworkRequestTournamentChoice(
            SessionId, resumed.Revision, resumed.CurrentMatchId, TournamentPlayerChoice.Join));
        if (withSuccessor)
        {
            Send(other, new NetworkRequestTournamentChoice(
                SessionId, resumed.Revision, resumed.CurrentMatchId, TournamentPlayerChoice.Join));
        }

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
        var (_, heroCharacterId) = SetUpTournament(host, other, otherIsSpectator: true);

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

        var notification = Assert.Single(host.InternalMessages.GetMessages<NetworkTournamentRequestRejected>());
        Assert.Contains("while you were disconnected", notification.Reason);
        Assert.Empty(other.InternalMessages.GetMessages<NetworkTournamentRequestRejected>());
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
        string otherCharacterId = other == null ? troopId : RegisterPlayer(OtherControllerId, settlementId);

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
