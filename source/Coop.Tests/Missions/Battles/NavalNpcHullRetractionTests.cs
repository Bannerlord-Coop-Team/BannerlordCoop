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

    // The take-back throws on the first hull's announcement before it reaches the second, then the battle host changes. The old
    // host announces neither as its own and doesn't settle; it hands both to the new host, which waits for its hull swaps,
    // takes them back as their simulator and only then settles the attempt as failed. Every copy goes once.
    [Fact]
    public void FailedFleet_HostChangesBeforeTheRetry_HandsAnUnvisitedHullToTheNewHost_WhichSettlesFailedAfterItWent()
    {
        var oldHost = new Harness();
        var newHost = new Harness(Peer, Own);
        var hulls = new[] { ShipTestHulls.Create(), ShipTestHulls.Create() };
        oldHost.LiveHulls.AddRange(hulls);
        var oldSpawner = oldHost.Spawner();
        var newSpawner = newHost.Spawner();
        var shipIds = new List<Guid>();
        bool sendFails = true;
        oldHost.FailSend = message =>
        {
            if (!sendFails || !(message is NetworkShipRemoved removal) || removal.ShipId != shipIds[0]) return false;
            sendFails = false;
            return true;
        };

        oldSpawner.AttemptFleet(work =>
        {
            foreach (var hull in hulls)
            {
                work.Hulls.Add(hull);
                shipIds.Add(oldHost.Replicator.RegisterNpcHull(hull, null, "MapEventParty_7"));
            }

            throw new InvalidOperationException("crewing threw");
        });
        var copies = newHost.AddCopies(shipIds, Own);
        Relay(oldHost, newHost, newSpawner);
        bool fieldedAgain = false;
        newSpawner.AttemptFleet(_ => fieldedAgain = true);

        Migrate(Peer, 1, oldHost, newHost);
        oldSpawner.Tick();

        Assert.False(fieldedAgain);
        Assert.Empty(oldHost.Sent.OfType<NetworkShipRemoved>());
        Assert.Empty(oldHost.Sent.OfType<NetworkNpcFleetAttempted>());
        Assert.Equal(BattleSideEnum.None, oldSpawner.FailedSide);

        Relay(oldHost, newHost, newSpawner);
        FinishTakeBack(newHost, newSpawner);
        Relay(newHost, oldHost, oldSpawner);
        oldHost.Replicator.Tick(0.1f);
        oldHost.Replicator.Tick(0.1f);

        Assert.Equal(shipIds.Select(id => "removed:" + id + ":" + Peer).Append("failed"), Settlement(newHost));
        Assert.Equal(hulls, oldHost.Removed);
        Assert.Equal(copies.Select(copy => newHost.Replacements[copy]), newHost.Removed);
        Assert.Equal(BattleSideEnum.Defender, oldSpawner.FailedSide);
        Assert.Equal(BattleSideEnum.Defender, newSpawner.FailedSide);
    }

    // The take-back's queue step throws on a hull, then the battle host changes. The old host doesn't announce it as the old
    // controller, which every peer would reject; the new host takes it back, and the old copy goes after its crew withdrew.
    [Fact]
    public void FailedFleet_HostChangesAfterAQueueStepThrew_TheNewHostTakesTheHullBack_AndPeersAcceptIt()
    {
        var oldHost = new Harness();
        var newHost = new Harness(Peer, Own);
        var hull = ShipTestHulls.Create();
        var crew = (Agent)RuntimeHelpers.GetUninitializedObject(typeof(Agent));
        var order = new List<string>();
        oldHost.LiveHulls.Add(hull);
        bool queueFails = true;
        oldHost.Engine.Setup(e => e.ReleaseHullStations(hull)).Callback(() =>
        {
            if (!queueFails) return;
            queueFails = false;
            throw new InvalidOperationException("stations threw");
        });
        oldHost.Engine.Setup(e => e.ParkHullAgents(hull)).Callback(() => order.Add("park"))
            .Returns(new[] { new HullSwapAgent(crew, isCrew: true, isParked: true, Vec3.Zero, Vec3.Zero) });
        oldHost.Engine.Setup(e => e.WithdrawAgent(crew)).Callback(() => order.Add("withdraw"));
        oldHost.Engine.Setup(e => e.RemoveHull(hull)).Callback(() => { order.Add("remove"); oldHost.LiveHulls.Remove(hull); });
        var oldSpawner = oldHost.Spawner();
        var newSpawner = newHost.Spawner();
        Guid shipId = Guid.Empty;

        oldSpawner.AttemptFleet(work =>
        {
            work.Hulls.Add(hull);
            shipId = oldHost.Replicator.RegisterNpcHull(hull, null, "MapEventParty_7");
            throw new InvalidOperationException("crewing threw");
        });
        var copy = newHost.AddCopies(new[] { shipId }, Own).Single();
        Migrate(Peer, 1, oldHost, newHost);
        oldSpawner.Tick();
        Relay(oldHost, newHost, newSpawner);
        FinishTakeBack(newHost, newSpawner);
        Relay(newHost, oldHost, oldSpawner);
        oldHost.Replicator.Tick(0.1f);
        oldHost.Replicator.Tick(0.1f);

        Assert.Empty(oldHost.Sent.OfType<NetworkShipRemoved>());
        Assert.Equal(new[] { "removed:" + shipId + ":" + Peer, "failed" }, Settlement(newHost));
        Assert.Equal(new[] { "park", "withdraw", "remove" }, order);
        Assert.Equal(new[] { newHost.Replacements[copy] }, newHost.Removed);
        Assert.Equal(BattleSideEnum.Defender, oldSpawner.FailedSide);
    }

    // Applies the host change on every client, the way each one's arrivals queue does.
    private static void Migrate(string newHost, int epoch, params Harness[] clients)
    {
        foreach (var client in clients)
            client.Replicator.TransferHostHulls(newHost, epoch);
    }

    // Lets the new host's hull swaps finish while its spawner retries, then runs its take-back's queued removals.
    private static void FinishTakeBack(Harness host, NavalNpcFleetSpawner spawner)
    {
        for (int tick = 0; tick < 4; tick++)
        {
            spawner.Tick();
            host.Replicator.Tick(0.1f);
        }

        host.Replicator.Tick(0.1f);
    }

    // Delivers what one client sent since the last relay to another, as its handlers apply it on the game thread.
    private static void Relay(Harness from, Harness to, NavalNpcFleetSpawner spawner)
    {
        foreach (var message in from.Sent.Skip(from.Relayed).ToList())
        {
            if (message is NetworkShipRemoved removal) to.Replicator.AcceptRemoval(removal);
            else if (message is NetworkNpcFleetTakeBack takeBack) spawner.AcceptTakeBack(takeBack);
            else if (message is NetworkNpcFleetAttempted attempted) spawner.AcceptAttempt(attempted);
        }

        from.Relayed = from.Sent.Count;
    }

    // The client's hull removals, by ship id and sender, and its fleet outcome, in send order.
    private static IEnumerable<string> Settlement(Harness client) => client.Sent
        .Where(message => message is NetworkShipRemoved || message is NetworkNpcFleetAttempted)
        .Select(message => message is NetworkShipRemoved removal
            ? "removed:" + removal.ShipId + ":" + removal.OwnerControllerId
            : ((NetworkNpcFleetAttempted)message).Failed ? "failed" : "fielded");
#endif

    // A replicator over a mocked engine and network, on one client (the battle host by default) with one peer in the mission.
    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public NetworkShipRegistry Registry { get; } = new NetworkShipRegistry();
        public List<MissionObject> LiveHulls { get; } = new List<MissionObject>();
        public Mock<IBattleSession> Session { get; } = new Mock<IBattleSession>();
        public ShipReplicatorFixture Fixture { get; }
        public BattleShipReplicator Replicator => Fixture.Replicator;
        // Every message sent to all peers, in order, unless FailSend made that send throw.
        public List<IMessage> Sent { get; } = new List<IMessage>();
        public int Relayed { get; set; }
        public Func<IMessage, bool> FailSend { get; set; } = _ => false;
        // Hulls the engine removed, in order, and the simulated hull a swap put in place of each copy.
        public List<MissionObject> Removed { get; } = new List<MissionObject>();
        public Dictionary<MissionObject, MissionObject> Replacements { get; } = new Dictionary<MissionObject, MissionObject>();

        public Harness(string own = Own, string peer = Peer)
        {
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(Registry);

            var session = Session;
            session.SetupGet(s => s.OwnControllerId).Returns(own);
            session.SetupGet(s => s.InstanceId).Returns("MapEvent_1");
            session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string id) => id == own);
            session.SetupGet(s => s.IsNaval).Returns(true);

            var deployment = new Mock<IBattleDeploymentCoordinator>();
            deployment.SetupGet(d => d.IsCommitted).Returns(true);

            Engine.SetupGet(e => e.Hulls).Returns(() => LiveHulls);
            Engine.Setup(e => e.AgentsOnDeck(It.IsAny<MissionObject>())).Returns(Array.Empty<Agent>());
            Engine.Setup(e => e.ParkHullAgents(It.IsAny<MissionObject>())).Returns(Array.Empty<HullSwapAgent>());
            Engine.Setup(e => e.GetFrame(It.IsAny<MissionObject>())).Returns(MatrixFrame.Identity);
            Engine.Setup(e => e.RemoveHull(It.IsAny<MissionObject>())).Callback<MissionObject>(hull =>
            {
                Removed.Add(hull);
                LiveHulls.Remove(hull);
            });
            Network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(message =>
            {
                if (FailSend(message)) throw new InvalidOperationException("send threw");
                Sent.Add(message);
            });

            var members = new Mock<IMissionContext>();
            members.SetupGet(m => m.ControllersInMission).Returns(new[] { peer });
            var context = ShipReplicatorFixture.Context(session.Object, component.Object, members.Object, deployment.Object);
            Fixture = new ShipReplicatorFixture(context, new MessageBroker(), Engine.Object, Network.Object);
        }

        // Registers this client's copies of another client's AI hulls; a host change here swaps each for a simulated hull.
        public IReadOnlyList<MissionObject> AddCopies(IEnumerable<Guid> shipIds, string authority)
        {
            var copies = new List<MissionObject>();
            foreach (var shipId in shipIds)
            {
                var copy = ShipTestHulls.Create();
                var simulated = ShipTestHulls.Create();
                Replacements[copy] = simulated;
                Engine.Setup(e => e.ReplaceHull(copy)).Returns(() =>
                {
                    LiveHulls.Remove(copy);
                    LiveHulls.Add(simulated);
                    return simulated;
                });
                LiveHulls.Add(copy);
                Registry.TryRegister(new NetworkShipInfo(shipId, authority, "MapEventParty_7", true, copy, null));
                copies.Add(copy);
            }

            return copies;
        }

#if COOP_NAVAL
        // The battle's AI fleet spawner, taking a failed attempt back through the real rollback and this replicator.
        public NavalNpcFleetSpawner Spawner() => new NavalNpcFleetSpawner(Network.Object, new MessageBroker(), Session.Object,
            Replicator, Mock.Of<ICoopShipSnapshotBuilder>(), Mock.Of<IObjectManager>(), Mock.Of<IPlayerManager>(),
            Mock.Of<INavalLateDeploymentPlanTracker>(), new NavalNpcFleetRollback(Replicator), BattleSideEnum.Defender);
#endif
    }
}
