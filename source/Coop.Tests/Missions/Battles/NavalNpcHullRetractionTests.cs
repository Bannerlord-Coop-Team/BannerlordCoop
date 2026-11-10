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
#if COOP_NAVAL
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions.Naval;
using TaleWorlds.Core;
#endif

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks the battle host taking back a failed AI fleet attempt's hulls through the ship replicator.</summary>
[Collection("Mission.Current")]
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

    // A take-back repeated before the parking, after it or after the removal leaves the one queued removal alone, so the hull
    // still goes only after its crew was parked and withdrawn (R33), and peers hear it once.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RetractNpcHulls_RepeatedAfterTicks_KeepsTheOneRemoval_AfterTheCrewWithdrew(int ticksBeforeRepeat)
    {
        var harness = new Harness();
        var hull = ShipTestHulls.Create();
        var crew = (Agent)RuntimeHelpers.GetUninitializedObject(typeof(Agent));
        var order = new List<string>();
        harness.Engine.Setup(e => e.ParkHullAgents(hull)).Callback(() => order.Add("park"))
            .Returns(new[] { new HullSwapAgent(crew, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero) });
        harness.Engine.Setup(e => e.WithdrawAgent(crew)).Callback(() => order.Add("withdraw"));
        harness.Engine.Setup(e => e.RemoveHull(hull)).Callback(() => { order.Add("remove"); harness.LiveHulls.Remove(hull); });
        harness.LiveHulls.Add(hull);
        var shipId = harness.Replicator.RegisterNpcHull(hull, null, "MapEventParty_7");

        harness.Replicator.RetractNpcHulls(new[] { hull });
        for (int tick = 0; tick < ticksBeforeRepeat; tick++) harness.Replicator.Tick(0.1f);
        harness.Replicator.RetractNpcHulls(new[] { hull });
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        Assert.Equal(new[] { "park", "withdraw", "remove" }, order);
        harness.Network.Verify(n => n.SendAll(It.Is<NetworkShipRemoved>(removed => removed.ShipId == shipId)), Times.Once);
    }

    // A hull that never registered was never announced, so peers hear nothing, but whoever boarded it is still parked and
    // withdrawn before it goes, as a registry miss says nothing about who is aboard.
    [Fact]
    public void RetractNpcHulls_AnUnregisteredHull_IsRemovedHereOnly_AfterItsCrewWithdrew()
    {
        var harness = new Harness();
        var hull = ShipTestHulls.Create();
        var crew = (Agent)RuntimeHelpers.GetUninitializedObject(typeof(Agent));
        var order = new List<string>();
        harness.Engine.Setup(e => e.ReleaseHullStations(hull)).Callback(() => order.Add("stations"));
        harness.Engine.Setup(e => e.ParkHullAgents(hull)).Callback(() => order.Add("park"))
            .Returns(new[] { new HullSwapAgent(crew, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero) });
        harness.Engine.Setup(e => e.WithdrawAgent(crew)).Callback(() => order.Add("withdraw"));
        harness.Engine.Setup(e => e.RemoveHull(hull)).Callback(() => order.Add("remove"));

        harness.Replicator.RetractNpcHulls(new[] { hull });
        harness.Replicator.RetractNpcHulls(new[] { hull });
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);

        Assert.Equal(new[] { "stations", "park", "withdraw", "remove" }, order);
        harness.Network.Verify(n => n.SendAll(It.IsAny<NetworkShipRemoved>()), Times.Never);
    }

#if COOP_NAVAL
    // A take-back that throws on the second hull's announcement keeps the attempt open; the next spawner tick resumes it, so
    // both hulls go once with their crew, peers hear each removal once, and only then that the attempt failed.
    [Fact]
    public void FailedFleet_TakeBackThrowsPartway_ResumesNextTick_AndSettlesFailedAfterEveryHullWent()
    {
        var harness = new Harness();
        var hulls = new[] { ShipTestHulls.Create(), ShipTestHulls.Create() };
        var removed = new List<MissionObject>();
        harness.Engine.Setup(e => e.RemoveHull(It.IsAny<MissionObject>()))
            .Callback<MissionObject>(hull => { removed.Add(hull); harness.LiveHulls.Remove(hull); });
        harness.LiveHulls.AddRange(hulls);
        var spawner = harness.Spawner();
        var shipIds = new List<Guid>();
        var sent = new List<string>();
        bool sendFails = true;
        harness.Network.Setup(n => n.SendAll(It.IsAny<NetworkShipRemoved>())).Callback<IMessage>(message =>
        {
            var removal = (NetworkShipRemoved)message;
            if (removal.ShipId == shipIds[1] && sendFails)
            {
                sendFails = false;
                throw new InvalidOperationException("send threw");
            }

            sent.Add("removed:" + shipIds.IndexOf(removal.ShipId));
        });
        harness.Network.Setup(n => n.SendAll(It.IsAny<NetworkNpcFleetAttempted>()))
            .Callback<IMessage>(message => sent.Add(((NetworkNpcFleetAttempted)message).Failed ? "failed" : "fielded"));

        spawner.AttemptFleet(work =>
        {
            foreach (var hull in hulls)
            {
                work.Hulls.Add(hull);
                shipIds.Add(harness.Replicator.RegisterNpcHull(hull, null, "MapEventParty_7"));
            }

            throw new InvalidOperationException("crewing threw");
        });

        Assert.Equal(new[] { "removed:0" }, sent);
        Assert.Equal(BattleSideEnum.None, spawner.FailedSide);

        spawner.Tick();
        harness.Replicator.Tick(0.1f);
        harness.Replicator.Tick(0.1f);
        spawner.Tick();

        Assert.Equal(new[] { "removed:0", "removed:1", "failed" }, sent);
        Assert.Equal(BattleSideEnum.Defender, spawner.FailedSide);
        Assert.Equal(hulls, removed);
        Assert.Empty(harness.LiveHulls);
    }
#endif

    // A replicator over a mocked engine and network, on the battle host with one peer in the mission.
    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public NetworkShipRegistry Registry { get; } = new NetworkShipRegistry();
        public List<MissionObject> LiveHulls { get; } = new List<MissionObject>();
        public Mock<IBattleSession> Session { get; } = new Mock<IBattleSession>();
        public ShipReplicatorFixture Fixture { get; }
        public BattleShipReplicator Replicator => Fixture.Replicator;

        public Harness()
        {
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(Registry);

            var session = Session;
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
            Fixture = new ShipReplicatorFixture(context, new MessageBroker(), Engine.Object, Network.Object);
        }

#if COOP_NAVAL
        // The battle's AI fleet spawner, taking a failed attempt back through the real rollback and this replicator.
        public NavalNpcFleetSpawner Spawner() => new NavalNpcFleetSpawner(Network.Object, new MessageBroker(), Session.Object,
            Replicator, Mock.Of<ICoopShipSnapshotBuilder>(), Mock.Of<IObjectManager>(), Mock.Of<IPlayerManager>(),
            Mock.Of<INavalLateDeploymentPlanTracker>(), new NavalNpcFleetRollback(Replicator), BattleSideEnum.Defender);
#endif
    }
}
