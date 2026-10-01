using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Extensions;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattleTroopHealthAuthorizationTests : MissionTestEnvironment
{
    public BattleTroopHealthAuthorizationTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Theory]
    [InlineData("other-party")]
    [InlineData("not-entered")]
    [InlineData("departed")]
    [InlineData("stale-connection")]
    [InlineData("disconnected")]
    [InlineData("wrong-battle")]
    [InlineData("missing-sender")]
    [InlineData("unknown-peer")]
    public void UnauthorizedReport_CannotRetainRoutedHealthAfterOwnerReport(string invalidSender)
    {
        var (battleId, partyId, entries) = PrepareBattle(ownerEntered: invalidSender != "not-entered");
        var owner = Clients.ElementAt(1);
        object sender = invalidSender == "other-party" ? Clients.First().NetPeer : owner.NetPeer;
        var report = HealthReport(battleId, partyId, entries, routedHealth: 1f);

        if (invalidSender == "departed") DepartBattle("owner", battleId);
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            if (invalidSender == "stale-connection") players.SetPeer("owner", NetPeerExtensions.CreatePeer());
            if (invalidSender == "disconnected") players.ClearPeer(owner.NetPeer);
            if (invalidSender == "missing-sender") sender = this;
            if (invalidSender == "unknown-peer") sender = NetPeerExtensions.CreatePeer();
            if (invalidSender == "wrong-battle")
            {
                var otherBattle = Server.CreateRegisteredObject<MapEvent>("other_battle");
                Assert.True(Server.ObjectManager.TryGetId(otherBattle, out var otherBattleId));
                report = HealthReport(otherBattleId, partyId, entries, routedHealth: 1f);
            }
            Server.Resolve<IMessageBroker>().Publish(sender, Server.EnsureSerializable(report));
        });

        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer("owner", owner.NetPeer);
        });
        if (invalidSender == "not-entered" || invalidSender == "departed") EnterBattle(owner, battleId);

        SendHealth(owner, HealthReport(battleId, partyId, entries, routedHealth: null));
        AssertRebuiltHealth(battleId, partyId, entries, routedHealth: null);
    }

    [Fact]
    public void ReplacedConnectionBeforeGameThreadApply_CannotRetainHealth()
    {
        var (battleId, partyId, entries) = PrepareBattle();
        var owner = Clients.ElementAt(1);
        var report = HealthReport(battleId, partyId, entries, routedHealth: 1f);
        Server.SimulateMessage(owner.NetPeer, report, markGameThread: false);
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer("owner", NetPeerExtensions.CreatePeer()));
        Server.PumpGameThread();
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer("owner", owner.NetPeer));

        SendHealth(owner, HealthReport(battleId, partyId, entries, routedHealth: null));
        AssertRebuiltHealth(battleId, partyId, entries, routedHealth: null);
    }

    [Fact]
    public void CurrentOwner_ReportIsRetainedBeforeRetreat()
    {
        var (battleId, partyId, entries) = PrepareBattle();
        SendHealth(Clients.ElementAt(1), HealthReport(battleId, partyId, entries, routedHealth: 37f));
        DepartBattle("owner", battleId, wasRetreat: true);

        AssertRebuiltHealth(battleId, partyId, entries, routedHealth: 37f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigratedHost_CanReportAdoptedPartyAndPreserveEarlierRoutedHealth(bool aiParty)
    {
        var (battleId, partyId, entries) = PrepareBattle(hostParty: true, aiParty: aiParty);
        SendHealth(Clients.First(), HealthReport(battleId, partyId, entries, routedHealth: 37f));
        DepartBattle("host", battleId);
        AssertHost(Server, battleId, "owner");

        SendHealth(Clients.First(), HealthReport(battleId, partyId, entries, routedHealth: 1f));
        SendHealth(Clients.ElementAt(1), HealthReport(battleId, partyId, entries, routedHealth: null));
        AssertRebuiltHealth(battleId, partyId, entries, routedHealth: 37f);
    }

    [Fact]
    public void ReturnedOwner_HostCanStillReportRoutedTroopsItAdopted()
    {
        var (battleId, partyId, entries) = PrepareBattle();
        DepartBattle("owner", battleId);
        EnterBattle(Clients.ElementAt(1), battleId);

        SendHealth(Clients.First(), HealthReport(battleId, partyId, entries, routedHealth: 37f));
        DepartBattle("host", battleId, wasRetreat: true);
        SendHealth(Clients.ElementAt(1), HealthReport(battleId, partyId, entries, routedHealth: null));
        AssertRebuiltHealth(battleId, partyId, entries, routedHealth: 37f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReturnedOwner_BeforePromotedHostRequestsReserves_PreservesAdoptedTroopHealth(bool ownerWasPromoted)
    {
        var (battleId, partyId, entries) = PrepareBattle(ownerEntered: false, successor: true);
        var owner = Clients.ElementAt(1);
        var successor = Clients.ElementAt(2);
        EnterBattle(ownerWasPromoted ? owner : successor, battleId);
        EnterBattle(ownerWasPromoted ? successor : owner, battleId);
        DepartBattle("host", battleId);
        AssertHost(Server, battleId, ownerWasPromoted ? "owner" : "successor",
            ownerWasPromoted ? "successor" : "owner");
        DepartBattle("owner", battleId);
        AssertHost(Server, battleId, "successor");
        EnterBattle(owner, battleId);

        SendHealth(successor, HealthReport(battleId, partyId, entries, routedHealth: 37f));
        SendHealth(owner, HealthReport(battleId, partyId, entries, routedHealth: null));
        AssertRebuiltHealth(battleId, partyId, entries, routedHealth: 37f);
    }

    private (string BattleId, string PartyId, TroopReserveEntry[] Entries) PrepareBattle(
        bool ownerEntered = true, bool hostParty = false, bool aiParty = false, bool successor = false)
    {
        var (battleId, _) = successor ? SetupCoopBattle("host", "owner", "successor") : SetupCoopBattle("host", "owner");
        var aiPartyId = aiParty ? CreateRegisteredObject<MobileParty>() : null;
        string partyId = null;
        TroopReserveEntry[] entries = null;
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            players.SetPeer("host", Clients.First().NetPeer);
            players.SetPeer("owner", Clients.ElementAt(1).NetPeer);
            if (successor) players.SetPeer("successor", Clients.ElementAt(2).NetPeer);
            var mapEvent = Server.GetRegisteredObject<MapEvent>(battleId);
            var party = hostParty ? mapEvent.AttackerSide.Parties[0] : mapEvent.DefenderSide.Parties[0];
            if (aiParty)
            {
                var mobileParty = Server.GetRegisteredObject<MobileParty>(aiPartyId);
                mobileParty.Party.MapEventSide = mapEvent.AttackerSide;
                party = mapEvent.AttackerSide.Parties.Single(value => value.Party == mobileParty.Party);
            }
            party.Party.MemberRoster.Clear();
            party.Party.MemberRoster.AddToCounts(Server.CreateRegisteredObject<CharacterObject>("routed_troop"), 1);
            party.Party.MemberRoster.AddToCounts(Server.CreateRegisteredObject<CharacterObject>("active_troop"), 1);
            party.Update();
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            entries = Server.Resolve<IBattleTroopReserveBuilder>()
                .GetOwnedReserves(mapEvent, hostParty ? "host" : "owner", isHost: hostParty)
                .SelectMany(side => side.Parties).Single(value => value.PartyId == partyId).Entries;
            Assert.Equal(2, entries.Length);
        }, MapEventDisabledMethods);
        EnterBattle(Clients.First(), battleId);
        if (ownerEntered) EnterBattle(Clients.ElementAt(1), battleId);
        return (battleId, partyId, entries);
    }

    private static NetworkBattleTroopHealth HealthReport(string battleId, string partyId,
        TroopReserveEntry[] entries, float? routedHealth)
    {
        var survivors = new Dictionary<int, float> { [entries[1].Seed] = 64f };
        Dictionary<int, float> routed = null;
        if (routedHealth.HasValue)
        {
            survivors[entries[0].Seed] = routedHealth.Value;
            routed = new Dictionary<int, float> { [entries[0].Seed] = routedHealth.Value };
        }
        return new NetworkBattleTroopHealth(battleId, partyId, survivors, 2, routed);
    }

    private static void SendHealth(EnvironmentInstance client, NetworkBattleTroopHealth report)
    {
        client.Call(() => client.Resolve<INetwork>().SendAll(report));
    }

    private void AssertRebuiltHealth(string battleId, string partyId, TroopReserveEntry[] entries, float? routedHealth)
    {
        Server.Call(() =>
        {
            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            var mapEvent = Server.GetRegisteredObject<MapEvent>(battleId);
            builder.ForgetMapEvent(mapEvent, preserveHealth: true);
            var rebuilt = builder.GetOwnedReserves(mapEvent, "owner", isHost: true, absentControllers: new[] { "host" })
                .SelectMany(side => side.Parties).Single(party => party.PartyId == partyId).Entries;
            Assert.Equal(routedHealth, rebuilt.Single(entry => entry.CharacterId == entries[0].CharacterId).Health);
            Assert.Equal(64f, rebuilt.Single(entry => entry.CharacterId == entries[1].CharacterId).Health);
        });
    }
}
