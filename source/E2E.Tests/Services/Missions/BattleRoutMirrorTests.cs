using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
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
