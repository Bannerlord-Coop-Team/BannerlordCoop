using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Regression coverage for peer-side battle rout application.</summary>
public class BattleRoutMirrorTests : MissionTestEnvironment
{
    public BattleRoutMirrorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RoutMetadata_RoundTripsOverTheWire()
    {
        var agentId = Guid.NewGuid();

        var message = Server.EnsureSerializable(new NetworkBattleAgentRouted(
            agentId,
            hideMount: false,
            isAdministrativeRemoval: true));

        Assert.Equal(agentId, message.AgentId);
        Assert.False(message.HideMount);
        Assert.True(message.IsAdministrativeRemoval);

        var departed = Server.EnsureSerializable(new NetworkBattleTroopDeparted(
            "map-event", "party", 1949));
        Assert.Equal("map-event", departed.MapEventId);
        Assert.Equal("party", departed.PartyId);
        Assert.Equal(1949, departed.TroopSeed);

        var prioritySlot = Server.EnsureSerializable(new NetworkBattlePrioritySlotAssigned(
            "map-event", 7, "waiting-party", "donor-party"));
        Assert.Equal("map-event", prioritySlot.MapEventId);
        Assert.Equal(7, prioritySlot.TransferId);
        Assert.Equal("waiting-party", prioritySlot.WaitingPartyId);
        Assert.Equal("donor-party", prioritySlot.DonorPartyId);

        var cancelledSlot = Server.EnsureSerializable(new NetworkBattlePrioritySlotCancelled(
            "map-event", 8, "cancelled-party", "restored-party"));
        Assert.Equal("map-event", cancelledSlot.MapEventId);
        Assert.Equal(8, cancelledSlot.TransferId);
        Assert.Equal("cancelled-party", cancelledSlot.WaitingPartyId);
        Assert.Equal("restored-party", cancelledSlot.DonorPartyId);

        var consumedSlot = Server.EnsureSerializable(new NetworkBattlePrioritySlotConsumed(
            "map-event", 9, "consuming-party"));
        Assert.Equal("map-event", consumedSlot.MapEventId);
        Assert.Equal(9, consumedSlot.TransferId);
        Assert.Equal("consuming-party", consumedSlot.WaitingPartyId);

        var settledSlot = Server.EnsureSerializable(new NetworkBattlePrioritySlotSettled(
            "map-event", 10, "settled-party"));
        Assert.Equal("map-event", settledSlot.MapEventId);
        Assert.Equal(10, settledSlot.TransferId);
        Assert.Equal("settled-party", settledSlot.WaitingPartyId);

        var snapshotReset = Server.EnsureSerializable(new NetworkBattlePrioritySnapshotReset(
            "map-event"));
        Assert.Equal("map-event", snapshotReset.MapEventId);

        var declinedSlot = Server.EnsureSerializable(new NetworkBattlePrioritySlotDeclined(
            "map-event", 11, "declining-party"));
        Assert.Equal("map-event", declinedSlot.MapEventId);
        Assert.Equal(11, declinedSlot.TransferId);
        Assert.Equal("declining-party", declinedSlot.WaitingPartyId);

        var queuedWait = Server.EnsureSerializable(new NetworkBattlePriorityWaitQueued(
            "map-event", "queued-party", resetExistingState: true));
        Assert.Equal("map-event", queuedWait.MapEventId);
        Assert.Equal("queued-party", queuedWait.WaitingPartyId);
        Assert.True(queuedWait.ResetExistingState);
    }

    [Fact]
    public void RoutedTroopDeparture_PublishesOneFreedHumanSlot()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var party = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));

            const int troopSeed = 1949;
            var ledger = Server.Resolve<IBattleTroopLedger>();
            ledger.SetReserve(mapEventId, partyId,
                new[] { new TroopReserveEntry(troopSeed, "character", formationClass: 0) });

            int slotsFreed = 0;
            var broker = Server.Resolve<IMessageBroker>();
            broker.Subscribe<BattleHumanSlotFreed>(_ => slotsFreed++);

            var departed = new NetworkBattleTroopDeparted(mapEventId, partyId, troopSeed);
            broker.Publish(this, departed);

            Assert.Equal(1, slotsFreed);
            Assert.Equal(new[] { troopSeed }, ledger.GetDepartedSeeds(mapEventId, partyId));
        });
    }

    [Fact]
    public void OwnedRout_RemembersTheDepartedReserveSeed()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        SetControllerId(owner, "owner");

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            var registry = owner.Resolve<INetworkAgentRegistry>();
            var broker = owner.Resolve<IMessageBroker>();
            var casualties = new CasualtyAttributionMap();
            var session = new BattleSession(
                owner.Resolve<IControllerIdProvider>(),
                owner.Resolve<IBattleHostRegistry>());
            Assert.True(session.TryBegin("map-event"));
            using var reporter = new AgentRoutReporter(
                owner.Resolve<IBattleNetwork>(),
                owner.Resolve<Common.Network.INetwork>(),
                broker,
                owner.Resolve<ICoopMissionComponent>(),
                session,
                casualties);

            var agent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.AI));
            var agentId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("owner", agentId, agent));
            casualties.Record(agentId, "party", 1949, "character");

            broker.Publish(this, new BattleAgentRouted(agent));

            Assert.True(casualties.WasDeparted(1949));
            Assert.False(registry.TryGetAgentInfo(agentId, out _));
            var departed = Assert.Single(owner.NetworkSentMessages.GetMessages<NetworkBattleTroopDeparted>());
            Assert.Equal("map-event", departed.MapEventId);
            Assert.Equal("party", departed.PartyId);
            Assert.Equal(1949, departed.TroopSeed);
        });
    }

    [Fact]
    public void RoutBeforeRegistration_AppliesWhenPendingRoutsDrain()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        var agentId = Guid.NewGuid();
        Agent peerAgent = null!;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var broker = peer.Resolve<IMessageBroker>();
            var casualties = new CasualtyAttributionMap();
            using var applier = new PuppetRoutApplier(
                broker,
                peer.Resolve<ICoopMissionComponent>(),
                casualties);

            broker.Publish(this, new NetworkBattleAgentRouted(
                agentId,
                hideMount: true,
                isAdministrativeRemoval: false));
            applier.DrainPendingRouts();

            peerAgent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(registry.TryRegisterAgent("owner", agentId, peerAgent));
            casualties.Record(agentId, "party", 1949, "character");

            applier.DrainPendingRouts();
            Assert.False(registry.TryGetAgentInfo(agentId, out _));
            Assert.True(casualties.WasDeparted(1949));
        });

        Assert.True(AgentMirror.TryGet(peerAgent, out var agentMirror));
        Assert.False(agentMirror.IsActive);
        Assert.False(agentMirror.WasKilled);
    }

    [Fact]
    public void AdministrativeRoutWithHideMountFalse_PreservesCurrentMountAndDoesNotMarkDeparted()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");

        Agent peerAgent = null!;
        Agent peerMount = null!;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var registry = peer.Resolve<INetworkAgentRegistry>();
            var broker = peer.Resolve<IMessageBroker>();
            var casualties = new CasualtyAttributionMap();
            using var applier = new PuppetRoutApplier(
                broker,
                peer.Resolve<ICoopMissionComponent>(),
                casualties);

            peerAgent = mock.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            peerMount = mock.SpawnMount(peerAgent);
            var agentId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("owner", agentId, peerAgent));
            casualties.Record(agentId, "party", 1949, "character");

            broker.Publish(this, new NetworkBattleAgentRouted(
                agentId,
                hideMount: false,
                isAdministrativeRemoval: true));

            Assert.False(registry.TryGetAgentInfo(agentId, out _));
            Assert.False(casualties.WasDeparted(1949));
        });

        Assert.True(AgentMirror.TryGet(peerAgent, out var agentMirror));
        Assert.False(agentMirror.IsActive);
        Assert.True(AgentMirror.TryGet(peerMount, out var mountMirror));
        Assert.True(mountMirror.IsActive);
    }
}
