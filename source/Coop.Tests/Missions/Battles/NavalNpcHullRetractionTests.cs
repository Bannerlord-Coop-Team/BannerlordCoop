using Common.Messaging;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks the battle host taking back a failed AI fleet attempt's hulls through the ship replicator.</summary>
public class NavalNpcHullRetractionTests
{
    private const string Own = "host";
    private const string Peer = "peer";

    // A failed attempt's hulls were already announced, so peers hear they leave with their crew, and here the crew withdraws
    // a tick after it is parked, before the hull goes (R33).
    [Fact]
    public void RetractNpcHulls_TellsPeersTheCrewLeaves_ThenWithdrawsTheCrewAndRemovesTheHull()
    {
        var harness = new Harness();
        var hull = ShipTestHulls.Create();
        var crew = (Agent)RuntimeHelpers.GetUninitializedObject(typeof(Agent));
        harness.Engine.Setup(e => e.ParkHullAgents(hull))
            .Returns(new[] { new HullSwapAgent(crew, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero) });
        var order = new List<string>();
        harness.Engine.Setup(e => e.WithdrawAgent(crew)).Callback(() => order.Add("withdraw"));
        harness.Engine.Setup(e => e.RemoveHull(hull)).Callback(() => order.Add("remove"));
        harness.LiveHulls.Add(hull);
        var shipId = harness.Replicator.RegisterNpcHull(hull, null, "MapEventParty_7");

        harness.Replicator.RetractNpcHulls(new[] { hull });
        harness.Replicator.Tick(0.1f);
        Assert.Empty(order);
        harness.Replicator.Tick(0.1f);

        harness.Network.Verify(n => n.SendAll(It.Is<NetworkShipRemoved>(removed => removed.ShipId == shipId
            && removed.OwnerControllerId == Own && removed.MissionEnding)), Times.Once);
        Assert.Equal(new[] { "withdraw", "remove" }, order);
        Assert.Equal(ShipLifecycle.Removed, harness.Registry.StateOf(shipId));
    }

    // Vanilla's removal event comes after the registry dropped the ship, so it announces nothing a second time.
    [Fact]
    public void RetractNpcHulls_VanillasRemovalEventLater_SendsNoSecondRemoval()
    {
        var harness = new Harness();
        var hull = ShipTestHulls.Create();
        harness.LiveHulls.Add(hull);
        harness.Replicator.RegisterNpcHull(hull, null, "MapEventParty_7");
        harness.Replicator.Tick(0.1f);

        harness.Replicator.RetractNpcHulls(new[] { hull });
        harness.Replicator.AnnounceRemoval(hull, ShipRemovalReason.Leave, missionEnding: false);

        harness.Network.Verify(n => n.SendAll(It.IsAny<NetworkShipRemoved>()), Times.Once);
    }

    // A hull that spawned but threw before it registered was never announced, so it goes here at once and peers hear nothing.
    [Fact]
    public void RetractNpcHulls_AnUnregisteredHull_IsRemovedHereOnly()
    {
        var harness = new Harness();
        var hull = ShipTestHulls.Create();
        var order = new List<string>();
        harness.Engine.Setup(e => e.ReleaseHullStations(hull)).Callback(() => order.Add("stations"));
        harness.Engine.Setup(e => e.RemoveHull(hull)).Callback(() => order.Add("remove"));

        harness.Replicator.RetractNpcHulls(new[] { hull });

        Assert.Equal(new[] { "stations", "remove" }, order);
        harness.Network.Verify(n => n.SendAll(It.IsAny<NetworkShipRemoved>()), Times.Never);
    }

    // A replicator over a mocked engine and network, on the battle host with one peer in the mission.
    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public NetworkShipRegistry Registry { get; } = new NetworkShipRegistry();
        public List<MissionObject> LiveHulls { get; } = new List<MissionObject>();
        public BattleShipReplicator Replicator { get; }

        public Harness()
        {
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(Registry);

            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.OwnControllerId).Returns(Own);
            session.SetupGet(s => s.InstanceId).Returns("MapEvent_1");
            session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string id) => id == Own);
            session.SetupGet(s => s.IsNaval).Returns(true);

            var deployment = new Mock<IBattleDeploymentCoordinator>();
            deployment.SetupGet(d => d.IsCommitted).Returns(true);

            Engine.SetupGet(e => e.Hulls).Returns(() => LiveHulls);
            Engine.Setup(e => e.AgentsOnDeck(It.IsAny<MissionObject>())).Returns(Array.Empty<Agent>());
            Engine.Setup(e => e.ParkHullAgents(It.IsAny<MissionObject>())).Returns(Array.Empty<HullSwapAgent>());
            Engine.Setup(e => e.GetFrame(It.IsAny<MissionObject>())).Returns(MatrixFrame.Identity);
            Engine.Setup(e => e.RemoveHull(It.IsAny<MissionObject>())).Callback<MissionObject>(hull => LiveHulls.Remove(hull));

            var members = new Mock<IMissionContext>();
            members.SetupGet(m => m.ControllersInMission).Returns(new[] { Peer });
            var context = ShipReplicatorFixture.Context(session.Object, component.Object, members.Object, deployment.Object);
            Replicator = new ShipReplicatorFixture(context, new MessageBroker(), Engine.Object, Network.Object).Replicator;
        }
    }
}
