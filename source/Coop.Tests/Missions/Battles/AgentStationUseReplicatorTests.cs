using Common.Messaging;
using Common;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class AgentStationUseReplicatorTests
{
    [Fact]
    public void Apply_DropsTheBufferedOwnerPoseBeforeSeatingThePuppet()
    {
        var calls = new System.Collections.Generic.List<string>();
        var movement = new Mock<IAgentMovementHandler>();
        movement.Setup(m => m.ForgetMovementTarget(It.IsAny<Agent>())).Callback(() => calls.Add("forget"));
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), true))
            .Callback(() => calls.Add("use"));
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentMovementHandler).Returns(movement.Object);
        component.SetupGet(c => c.AgentActionHandler).Returns(Mock.Of<IAgentActionHandler>());

        using var replicator = new AgentStationUseReplicator(Mock.Of<IBattleNetwork>(), new MessageBroker(),
            Mock.Of<IBattleSession>(), component.Object, engine.Object, Mock.Of<IBattleDeploymentCoordinator>(), Members("owner"));
        replicator.Apply(null!, null!, inUse: true);

        Assert.Equal(new[] { "forget", "use" }, calls);
    }

    [Fact]
    public void RefreshAppliedSeats_RepinsAPuppetTheOwnerKeepsSeatedEveryTick()
    {
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(true);
        engine.Setup(e => e.IsSeated(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>())).Returns(true);
        using var replicator = CreateReplicator(engine.Object);
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, null!, CreatePoint());

        replicator.RefreshAppliedSeats();
        replicator.RefreshAppliedSeats();

        engine.Verify(e => e.PinToStation(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>()), Times.Exactly(2));
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void RefreshAppliedSeats_ReseatsALocalReleaseOnlyOncePerOwnerRevision()
    {
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(true);
        engine.Setup(e => e.IsSeated(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>())).Returns(false);
        var hull = ShipTestHulls.Create();
        var point = CreatePoint();
        StationOf(engine, point, hull);
        using var replicator = CreateReplicator(engine.Object);
        var agentId = Guid.NewGuid();
        replicator.RecordAppliedSeat(agentId, null!, hull, point);

        replicator.RefreshAppliedSeats();
        replicator.RefreshAppliedSeats();
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), point, true), Times.Once);

        replicator.RecordAppliedSeat(agentId, null!, hull, point);
        replicator.RefreshAppliedSeats();
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), point, true), Times.Exactly(2));
        engine.Verify(e => e.PinToStation(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>()), Times.Never);
    }

    [Fact]
    public void RefreshAppliedSeats_NeverReseatsOntoAHullThatLeftTheMission()
    {
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(true);
        engine.Setup(e => e.IsSeated(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>())).Returns(false);
        var hull = ShipTestHulls.Create();
        engine.Setup(e => e.IsHullRemoved(hull)).Returns(true);
        using var replicator = CreateReplicator(engine.Object);
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, hull, CreatePoint());

        replicator.RefreshAppliedSeats();
        engine.Setup(e => e.IsHullRemoved(hull)).Returns(false);
        replicator.RefreshAppliedSeats();

        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);
        engine.Verify(e => e.PinToStation(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>()), Times.Never);
        Assert.Equal(1, (long)JObject.FromObject(replicator.Inspect())["removedHullSeats"]!);
    }

    // M8a, R33: every puppet seated on a departing hull is released before the hull goes, and seats on other hulls stay.
    [Fact]
    public void ReleaseSeatsOn_ReleasesOnlyThePuppetsSeatedOnThatHull()
    {
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsSeated(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>())).Returns(true);
        var departing = ShipTestHulls.Create();
        var staying = ShipTestHulls.Create();
        var departingPoint = CreatePoint();
        var stayingPoint = CreatePoint();
        using var replicator = CreateReplicator(engine.Object);
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, departing, departingPoint);
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, staying, stayingPoint);

        int released = replicator.ReleaseSeatsOn(departing);

        Assert.Equal(1, released);
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), departingPoint, false), Times.Once);
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), stayingPoint, It.IsAny<bool>()), Times.Never);
        var inspected = JObject.FromObject(replicator.Inspect());
        Assert.Equal(1, (int)inspected["appliedSeats"]!);
        Assert.Equal(1, (long)inspected["departedHullReleases"]!);
    }

    // S12 A4 (NB §6 relayed catch-up): a relayed seat can beat its sender's entry, so it waits for the membership.
    [Fact]
    public void TryApply_RetriesAUseFromASenderNotYetInTheMission()
    {
        var crew = new RemoteCrew();
        var members = new System.Collections.Generic.List<string> { "someone_else" };
        var context = new Mock<IMissionContext>();
        context.SetupGet(m => m.ControllersInMission).Returns(() => members.ToArray());
        using var replicator = CreateReplicator(crew.Engine.Object, crew.Registry.Object, crew.Ships, members: context.Object);
        var use = Use(crew.AgentId, crew.ShipId, inUse: true, revision: 1, "owner");

        bool settledBeforeEntry = replicator.TryApply(use);
        members.Add("owner");
        bool settledAfterEntry = replicator.TryApply(use);

        Assert.False(settledBeforeEntry);
        Assert.True(settledAfterEntry);
        crew.Engine.Verify(e => e.ApplyStationUse(crew.Agent, crew.Point, true), Times.Once);
        Assert.Equal(0, (long)JObject.FromObject(replicator.Inspect())["dropped"]!);
    }

    // S12 R1: a departed host's last use for an agent this client adopted settles as stale at once, not with the expiry Warning.
    [Fact]
    public void Tick_DropsADepartedHostsInFlightUseForAnAdoptedAgentWithoutWaiting()
    {
        var crew = new RemoteCrew();
        crew.Registry.Setup(r => r.IsLocallyControlled(crew.Agent)).Returns(true);
        using var replicator = CreateReplicator(crew.Engine.Object, crew.Registry.Object, crew.Ships, members: Members("someone_else"));

        replicator.AcceptUse(Use(crew.AgentId, crew.ShipId, inUse: true, revision: 7, "owner"));
        replicator.Tick(0f);

        var inspected = JObject.FromObject(replicator.Inspect());
        Assert.Equal(0, (int)inspected["pending"]!);
        Assert.Equal(1, (long)inspected["staleUses"]!);
        Assert.Equal(0, (long)inspected["dropped"]!);
        crew.Engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);
    }

    // S12 A3: a joiner's seats go out from the tick, after the arrivals sent it the hull records they refer to.
    [Fact]
    public void PeerEntered_SendsTheJoinerTheOwnSeatsFromTheNextTick()
    {
        var agentId = Guid.NewGuid();
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var info = new CoopAgentInfo("local", "local", "battle", agent, agentId, 1);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.TryGetAgentInfo(agent, out info)).Returns(true);
        registry.Setup(r => r.IsLocallyControlled(agent)).Returns(true);
        registry.Setup(r => r.IsLocallyControlled(agentId)).Returns(true);
        var hull = ShipTestHulls.Create();
        var ships = new NetworkShipRegistry();
        ships.TryRegister(new NetworkShipInfo(Guid.NewGuid(), "local", "party", isNpcParty: false, hull, null!));
        var point = CreatePoint();
        var engine = new Mock<INavalShipEngine>();
        StationOf(engine, point, hull);
        engine.Setup(e => e.IsSeated(agent, point)).Returns(true);
        var network = new Mock<IBattleNetwork>();
        var deployment = new Mock<IBattleDeploymentCoordinator>();
        deployment.SetupGet(d => d.IsCommitted).Returns(true);
        var broker = new MessageBroker();
        using var replicator = CreateReplicator(engine.Object, registry.Object, ships, broker, network: network.Object,
            deployment: deployment.Object);
        GameThread.Run(() => broker.Publish(this, new AgentStationUseChanged(agent, point, inUse: true)), blocking: true);
        replicator.Tick(0f);

        GameThread.Run(() => broker.Publish(this, new NetworkMissionPeerEntered("joiner", null)), blocking: true);
        network.Verify(n => n.Send("joiner", It.IsAny<NetworkAgentStationUse>()), Times.Never);
        replicator.Tick(0f);
        replicator.Tick(0f);

        network.Verify(n => n.Send("joiner", It.Is<NetworkAgentStationUse>(use => use.AgentId == agentId && use.InUse)), Times.Once);
    }

    [Fact]
    public void TryApply_DropsAUseOnAHullThatLeftTheMission()
    {
        var agentId = Guid.NewGuid();
        var shipId = Guid.NewGuid();
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var info = new CoopAgentInfo("owner", "owner", "battle", agent, agentId, 1);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.TryGetAgentInfo(agentId, out info)).Returns(true);
        var hull = ShipTestHulls.Create();
        var ship = new NetworkShipInfo(shipId, "owner", "party", isNpcParty: false, hull, null!);
        var ships = new Mock<INetworkShipRegistry>();
        ships.Setup(s => s.TryGet(shipId, out ship)).Returns(true);
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsHullRemoved(hull)).Returns(true);
        engine.Setup(e => e.ResolveStation(hull, "oar", 0)).Returns(CreatePoint());
        using var replicator = CreateReplicator(engine.Object, registry.Object, ships.Object);

        Assert.True(replicator.TryApply(Use(agentId, shipId, inUse: true, revision: 1, "owner")));

        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);
        Assert.Equal(1, (long)JObject.FromObject(replicator.Inspect())["dropped"]!);
    }

    [Theory]
    [InlineData(9.9f, 0f, 0f, false)]
    [InlineData(10f, 0f, 0f, true)]
    [InlineData(30f, 0f, 25f, false)]
    [InlineData(35f, 0f, 25f, true)]
    [InlineData(30f, 22f, 10f, false)]
    public void IsPendingExpired_CountsTheLifetimeFromWhenLocalDeploymentLastHeldTheCopies(float elapsed, float receivedAt,
        float deploymentHeldAt, bool expected)
    {
        Assert.Equal(expected, AgentStationUseReplicator.IsPendingExpired(elapsed, receivedAt, deploymentHeldAt));
    }

    [Fact]
    public void Tick_KeepsAUseForAHeldHullPendingThroughALongDeployment()
    {
        var agentId = Guid.NewGuid();
        var shipId = Guid.NewGuid();
        var engine = new Mock<INavalShipEngine>();
        bool deploying = true;
        engine.SetupGet(e => e.IsDeploymentMode).Returns(() => deploying);
        using var replicator = CreateReplicator(engine.Object, Mock.Of<INetworkAgentRegistry>());
        replicator.Tick(0.1f);
        replicator.AcceptUse(Use(agentId, shipId, inUse: true, revision: 1, "owner"));

        for (int i = 0; i < 30; i++) replicator.Tick(1f);
        long droppedWhileDeploying = (long)JObject.FromObject(replicator.Inspect())["dropped"]!;
        deploying = false;
        for (int i = 0; i < 9; i++) replicator.Tick(1f);
        long droppedJustAfter = (long)JObject.FromObject(replicator.Inspect())["dropped"]!;
        replicator.Tick(1.5f);

        Assert.Equal(0, droppedWhileDeploying);
        Assert.Equal(0, droppedJustAfter);
        Assert.Equal(1, (long)JObject.FromObject(replicator.Inspect())["dropped"]!);
    }

    [Fact]
    public void Tick_DropsAPendingUseOnceItsHullsSpawnFailed()
    {
        var shipId = Guid.NewGuid();
        var ships = new NetworkShipRegistry();
        using var replicator = CreateReplicator(Mock.Of<INavalShipEngine>(), Mock.Of<INetworkAgentRegistry>(), ships);
        replicator.AcceptUse(Use(Guid.NewGuid(), shipId, inUse: true, revision: 1, "owner"));
        replicator.Tick(0.1f);
        long droppedWhileAwaiting = (long)JObject.FromObject(replicator.Inspect())["dropped"]!;

        ships.MarkSpawnFailed(shipId);
        replicator.Tick(0.1f);

        var inspected = JObject.FromObject(replicator.Inspect());
        Assert.Equal(0, droppedWhileAwaiting);
        Assert.Equal(1, (long)inspected["dropped"]!);
        Assert.Equal(0, (int)inspected["pending"]!);
    }

    [Fact]
    public void RefreshAppliedSeats_ForgetsAPuppetThatIsNoLongerActive()
    {
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(false);
        using var replicator = CreateReplicator(engine.Object);
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, null!, CreatePoint());

        replicator.RefreshAppliedSeats();
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(true);
        replicator.RefreshAppliedSeats();

        engine.Verify(e => e.PinToStation(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>()), Times.Never);
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void RefreshAppliedSeats_ReleasesAndForgetsAPuppetSeatedOnASinkingHull()
    {
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(true);
        engine.Setup(e => e.IsSeated(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>())).Returns(true);
        var hull = ShipTestHulls.Create();
        engine.Setup(e => e.IsSinking(hull)).Returns(true);
        using var replicator = CreateReplicator(engine.Object);
        var point = CreatePoint();
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, hull, point);

        replicator.RefreshAppliedSeats();
        replicator.RefreshAppliedSeats();

        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), point, false), Times.Once);
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), true), Times.Never);
        engine.Verify(e => e.PinToStation(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>()), Times.Never);
    }

    [Fact]
    public void RefreshAppliedSeats_ForgetsAPuppetThatDiedSeatedWithoutReleasingIt()
    {
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(false);
        var hull = ShipTestHulls.Create();
        engine.Setup(e => e.IsSinking(hull)).Returns(true);
        using var replicator = CreateReplicator(engine.Object);
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, hull, CreatePoint());

        replicator.RefreshAppliedSeats();

        // While the mission runs, vanilla Agent.OnRemove stops a removed agent using its station.
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void RefreshAppliedSeats_ReleasesAndForgetsAWithdrawnPuppetStillSeated()
    {
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(true);
        engine.Setup(e => e.IsSeated(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>())).Returns(true);
        using var replicator = CreateReplicator(engine.Object, Mock.Of<INetworkAgentRegistry>());
        var point = CreatePoint();
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, null!, point);

        replicator.RefreshAppliedSeats();
        replicator.RefreshAppliedSeats();

        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), point, false), Times.Once);
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), true), Times.Never);
        engine.Verify(e => e.PinToStation(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>()), Times.Never);
    }

    [Fact]
    public void RefreshAppliedSeats_NeverReseatsAWithdrawnPuppetThatWasAlreadyReleased()
    {
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(true);
        engine.Setup(e => e.IsSeated(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>())).Returns(false);
        using var replicator = CreateReplicator(engine.Object, Mock.Of<INetworkAgentRegistry>());
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, null!, CreatePoint());

        replicator.RefreshAppliedSeats();
        replicator.RefreshAppliedSeats();

        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);
        engine.Verify(e => e.PinToStation(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>()), Times.Never);
    }

    // Every agent is registered to a remote owner, so an applied seat stays this client's to follow.
    private static INetworkAgentRegistry RemoteOwnerRegistry()
    {
        var info = new CoopAgentInfo("owner", "owner", "battle", null!, Guid.Empty, 1);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.TryGetAgentInfo(It.IsAny<Guid>(), out info)).Returns(true);
        return registry.Object;
    }

    private static AgentStationUseReplicator CreateReplicator(INavalShipEngine engine,
        INetworkAgentRegistry? registry = null, INetworkShipRegistry? ships = null, MessageBroker? broker = null,
        IMissionContext? members = null, IBattleNetwork? network = null, IBattleDeploymentCoordinator? deployment = null,
        IBattleSession? session = null)
    {
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());
        component.SetupGet(c => c.AgentActionHandler).Returns(Mock.Of<IAgentActionHandler>());
        component.SetupGet(c => c.AgentRegistry).Returns(registry ?? RemoteOwnerRegistry());
        component.SetupGet(c => c.ShipRegistry).Returns(ships ?? Mock.Of<INetworkShipRegistry>());
        return new AgentStationUseReplicator(network ?? Mock.Of<IBattleNetwork>(), broker ?? new MessageBroker(),
            session ?? Mock.Of<IBattleSession>(), component.Object, engine, deployment ?? Mock.Of<IBattleDeploymentCoordinator>(),
            members ?? Members("owner", "old", "new"));
    }

    // The server-announced mission members the membership gate reads.
    private static IMissionContext Members(params string[] controllerIds)
    {
        var members = new Mock<IMissionContext>();
        members.SetupGet(m => m.ControllersInMission).Returns(controllerIds);
        return members.Object;
    }

    // A distinct point identity; the engine mock never touches it.
    private static UsableMissionObject CreatePoint() => ShipTestHulls.CreatePoint();

    // Makes the engine describe the point as a station of the hull.
    private static void StationOf(Mock<INavalShipEngine> engine, UsableMissionObject point, MissionObject hull)
    {
        var stationHull = hull;
        string stationKey = "oar";
        int pointIndex = 0;
        engine.Setup(e => e.TryDescribeStation(point, out stationHull, out stationKey, out pointIndex)).Returns(true);
    }

    [Fact]
    public void RefreshAppliedSeats_NeverReseatsAStationThatLeftItsHull()
    {
        var engine = new Mock<INavalShipEngine>();
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(true);
        engine.Setup(e => e.IsSeated(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>())).Returns(false);
        var hull = ShipTestHulls.Create();
        var detached = CreatePoint();
        var movedAway = CreatePoint();
        StationOf(engine, movedAway, ShipTestHulls.Create());
        using var replicator = CreateReplicator(engine.Object);
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, hull, detached);
        replicator.RecordAppliedSeat(Guid.NewGuid(), null!, hull, movedAway);

        replicator.RefreshAppliedSeats();
        StationOf(engine, detached, hull);
        replicator.RefreshAppliedSeats();

        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);
        engine.Verify(e => e.PinToStation(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>()), Times.Never);
        var inspected = JObject.FromObject(replicator.Inspect());
        Assert.Equal(2, (long)inspected["detachedSeats"]!);
        Assert.Equal(0, (int)inspected["appliedSeats"]!);
    }

    [Fact]
    public void TryApply_AcceptsTheNewOwnersRestartedRevisionsAfterAMigration()
    {
        var agentId = Guid.NewGuid();
        var shipId = Guid.NewGuid();
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var info = new CoopAgentInfo("old", "old", "battle", agent, agentId, 1);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.TryGetAgentInfo(agentId, out info)).Returns(true);
        var hull = ShipTestHulls.Create();
        var ship = new NetworkShipInfo(shipId, "old", "party", isNpcParty: true, hull, null!);
        var ships = new Mock<INetworkShipRegistry>();
        ships.Setup(s => s.TryGet(shipId, out ship)).Returns(true);
        var engine = new Mock<INavalShipEngine>();
        var point = CreatePoint();
        engine.Setup(e => e.ResolveStation(hull, "oar", 0)).Returns(point);
        engine.Setup(e => e.IsSeated(agent, point)).Returns(true);
        using var replicator = CreateReplicator(engine.Object, registry.Object, ships.Object);

        Assert.True(replicator.TryApply(Use(agentId, shipId, inUse: true, revision: 3, "old")));
        Assert.False(replicator.TryApply(Use(agentId, shipId, inUse: false, revision: 1, "new")));

        info.CurrentAuthority = "new";
        Assert.True(replicator.TryApply(Use(agentId, shipId, inUse: false, revision: 1, "new")));
        Assert.True(replicator.TryApply(Use(agentId, shipId, inUse: true, revision: 2, "new")));
        Assert.False(replicator.TryApply(Use(agentId, shipId, inUse: false, revision: 4, "old")));

        engine.Verify(e => e.ApplyStationUse(agent, point, true), Times.Exactly(2));
        engine.Verify(e => e.ApplyStationUse(agent, point, false), Times.Once);
    }

    [Fact]
    public void Tick_ForgetsAnOwnSeatOnceTheAgentsAuthorityMovesAway()
    {
        var agentId = Guid.NewGuid();
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var info = new CoopAgentInfo("local", "local", "battle", agent, agentId, 1);
        bool locallyControlled = true;
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.TryGetAgentInfo(agent, out info)).Returns(true);
        registry.Setup(r => r.IsLocallyControlled(agent)).Returns(() => locallyControlled);
        registry.Setup(r => r.IsLocallyControlled(agentId)).Returns(() => locallyControlled);
        var hull = ShipTestHulls.Create();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "local", "party", isNpcParty: false, hull, null!);
        var ships = new Mock<INetworkShipRegistry>();
        ships.Setup(s => s.TryGetByHull(hull, out ship)).Returns(true);
        var point = CreatePoint();
        var engine = new Mock<INavalShipEngine>();
        var stationHull = (MissionObject)hull;
        string stationKey = "oar";
        int pointIndex = 0;
        bool? describedOnGameThread = null;
        engine.Setup(e => e.TryDescribeStation(point, out stationHull, out stationKey, out pointIndex))
            .Callback(() => describedOnGameThread = GameThread.Instance.IsGameThread)
            .Returns(true);
        engine.Setup(e => e.IsSeated(agent, point)).Returns(true);
        var broker = new MessageBroker();
        using var replicator = CreateReplicator(engine.Object, registry.Object, ships.Object, broker);

        // R41a-2 C6: agent ticks can run on a worker, so a use published off the game thread is announced on it.
        bool publishedOnGameThread = GameThread.Instance.IsGameThread;
        broker.Publish(this, new AgentStationUseChanged(agent, point, inUse: true));
        GameThread.Run(() => { }, blocking: true);
        replicator.Tick(0.1f);
        bool seatedWhileOwned = replicator.IsOwnSeat(agentId);
        locallyControlled = false;
        replicator.Tick(0.1f);

        Assert.False(publishedOnGameThread);
        Assert.True(describedOnGameThread);
        Assert.True(seatedWhileOwned);
        Assert.False(replicator.IsOwnSeat(agentId));
    }

    [Fact]
    public void DeferredUse_OvertakenByAnInlineRelease_IsDroppedAndLeavesNoOwnSeat()
    {
        var agentId = Guid.NewGuid();
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var info = new CoopAgentInfo("local", "local", "battle", agent, agentId, 1);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.TryGetAgentInfo(agent, out info)).Returns(true);
        registry.Setup(r => r.IsLocallyControlled(agent)).Returns(true);
        registry.Setup(r => r.IsLocallyControlled(agentId)).Returns(true);
        var hull = ShipTestHulls.Create();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "local", "party", isNpcParty: false, hull, null!);
        var ships = new Mock<INetworkShipRegistry>();
        ships.Setup(s => s.TryGetByHull(hull, out ship)).Returns(true);
        var point = CreatePoint();
        var engine = new Mock<INavalShipEngine>();
        StationOf(engine, point, hull);
        bool seated = true;
        engine.Setup(e => e.IsSeated(agent, point)).Returns(() => seated);
        var broker = new MessageBroker();
        using var replicator = CreateReplicator(engine.Object, registry.Object, ships.Object, broker);
        GameThread.Run(() => broker.Publish(this, new AgentStationUseChanged(agent, point, inUse: true)), blocking: true);
        bool seatedBefore = replicator.IsOwnSeat(agentId);

        // A worker queues a use; before it drains, the game thread releases the agent inline.
        GameThread.Run(() =>
        {
            Task.Run(() => broker.Publish(this, new AgentStationUseChanged(agent, point, inUse: true))).Wait();
            seated = false;
            broker.Publish(this, new AgentStationUseChanged(agent, point, inUse: false));
        }, blocking: true);
        GameThread.Run(() => { }, blocking: true);

        Assert.True(seatedBefore);
        Assert.False(replicator.IsOwnSeat(agentId));
    }

    [Fact]
    public void AnnounceStationUse_QueuedBeforeDispose_DoesNothing()
    {
        var agentId = Guid.NewGuid();
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var info = new CoopAgentInfo("local", "local", "battle", agent, agentId, 1);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.TryGetAgentInfo(agent, out info)).Returns(true);
        registry.Setup(r => r.IsLocallyControlled(agent)).Returns(true);
        var hull = ShipTestHulls.Create();
        var ship = new NetworkShipInfo(Guid.NewGuid(), "local", "party", isNpcParty: false, hull, null!);
        var ships = new Mock<INetworkShipRegistry>();
        ships.Setup(s => s.TryGetByHull(hull, out ship)).Returns(true);
        var point = CreatePoint();
        var engine = new Mock<INavalShipEngine>();
        StationOf(engine, point, hull);
        engine.Setup(e => e.IsSeated(agent, point)).Returns(true);
        var broker = new MessageBroker();
        var replicator = CreateReplicator(engine.Object, registry.Object, ships.Object, broker);

        GameThread.Run(() =>
        {
            Task.Run(() => broker.Publish(this, new AgentStationUseChanged(agent, point, inUse: true))).Wait();
            replicator.Dispose();
        }, blocking: true);
        GameThread.Run(() => { }, blocking: true);

        Assert.False(replicator.IsOwnSeat(agentId));
    }

    [Fact]
    public void Inspect_SeparatesOwnerReleasesFromSinkingReleases()
    {
        var agentId = Guid.NewGuid();
        var shipId = Guid.NewGuid();
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var info = new CoopAgentInfo("owner", "owner", "battle", agent, agentId, 1);
        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(r => r.TryGetAgentInfo(It.IsAny<Guid>(), out info)).Returns(true);
        var hull = ShipTestHulls.Create();
        var ship = new NetworkShipInfo(shipId, "owner", "party", isNpcParty: true, hull, null!);
        var ships = new Mock<INetworkShipRegistry>();
        ships.Setup(s => s.TryGet(shipId, out ship)).Returns(true);
        var engine = new Mock<INavalShipEngine>();
        var point = CreatePoint();
        engine.Setup(e => e.ResolveStation(hull, "oar", 0)).Returns(point);
        engine.Setup(e => e.IsAlive(It.IsAny<Agent>())).Returns(true);
        engine.Setup(e => e.IsSeated(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>())).Returns(true);
        using var replicator = CreateReplicator(engine.Object, registry.Object, ships.Object);

        replicator.TryApply(Use(agentId, shipId, inUse: true, revision: 1, "owner"));
        replicator.TryApply(Use(agentId, shipId, inUse: false, revision: 2, "owner"));
        engine.Setup(e => e.IsSinking(hull)).Returns(true);
        replicator.TryApply(Use(agentId, shipId, inUse: true, revision: 3, "owner"));
        replicator.RecordAppliedSeat(agentId, agent, hull, point);
        replicator.RefreshAppliedSeats();

        var inspected = JObject.FromObject(replicator.Inspect());
        Assert.Equal(1, (long)inspected["ownerReleases"]!);
        Assert.Equal(1, (long)inspected["sinkingUseReleases"]!);
        Assert.Equal(1, (long)inspected["sinkingSafetyNetReleases"]!);
    }

    // R48: seating from the game-thread queue overlapped the engine's async agent tick, so a received use waits for the tick.
    [Fact]
    public void AcceptUse_SeatsThePuppetOnlyFromTheNextTick()
    {
        var crew = new RemoteCrew();
        using var replicator = crew.CreateReplicator();

        replicator.AcceptUse(Use(crew.AgentId, crew.ShipId, inUse: true, revision: 1, "owner"));
        crew.Engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);

        replicator.Tick(0f);
        crew.Engine.Verify(e => e.ApplyStationUse(crew.Agent, crew.Point, true), Times.Once);
    }

    // S12 D1: the death deregisters a seated puppet before the owner's release applies, which then has nothing to do.
    [Fact]
    public void Tick_SettlesTheReleaseOfAPuppetThatDiedSeated_AndRejectsItsOlderUses()
    {
        var crew = new RemoteCrew();
        using var replicator = crew.CreateReplicator();
        replicator.AcceptUse(Use(crew.AgentId, crew.ShipId, inUse: true, revision: 1, "owner"));
        replicator.Tick(0f);

        crew.Registered = false;
        replicator.AcceptUse(Use(crew.AgentId, crew.ShipId, inUse: false, revision: 2, "owner"));
        replicator.Tick(0f);
        var settled = JObject.FromObject(replicator.Inspect());
        crew.Registered = true;
        bool olderSettled = replicator.TryApply(Use(crew.AgentId, crew.ShipId, inUse: true, revision: 1, "owner"));

        Assert.Equal(0, (int)settled["pending"]!);
        Assert.Equal(0, (long)settled["dropped"]!);
        Assert.Equal(1, (long)settled["obsoleteReleases"]!);
        Assert.True(olderSettled);
        Assert.Equal(1, (long)JObject.FromObject(replicator.Inspect())["staleUses"]!);
        crew.Engine.Verify(e => e.ApplyStationUse(crew.Agent, crew.Point, true), Times.Once);
        crew.Engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), false), Times.Never);
    }

    // S12 D2: the owner sends its crew's releases after the hull removal, which already let the crew go here.
    [Fact]
    public void Tick_SettlesACrewReleaseThatArrivesAfterItsHullLeft_ButDropsASeat()
    {
        var crew = new RemoteCrew();
        using var replicator = crew.CreateReplicator();
        replicator.AcceptUse(Use(crew.AgentId, crew.ShipId, inUse: true, revision: 1, "owner"));
        replicator.Tick(0f);

        crew.Ships.Remove(crew.ShipId);
        replicator.AcceptUse(Use(crew.AgentId, crew.ShipId, inUse: false, revision: 2, "owner"));
        replicator.AcceptUse(Use(Guid.NewGuid(), crew.ShipId, inUse: true, revision: 1, "owner"));
        replicator.Tick(0f);

        var inspected = JObject.FromObject(replicator.Inspect());
        Assert.Equal(0, (int)inspected["pending"]!);
        Assert.Equal(1, (long)inspected["obsoleteReleases"]!);
        Assert.Equal(1, (long)inspected["dropped"]!);
    }

    // R50 F2: vanilla releases a live puppet for the same causes as its owner (a ramp over an oar), so the release alone is no desync.
    [Fact]
    public void AnnounceStationUse_LeavesAPuppetsLocalReleaseToTheRefresh()
    {
        var crew = new RemoteCrew();
        crew.Engine.Setup(e => e.IsAlive(crew.Agent)).Returns(true);
        var broker = new MessageBroker();
        var network = new Mock<IBattleNetwork>();
        using var replicator = CreateReplicator(crew.Engine.Object, crew.Registry.Object, crew.Ships, broker, network: network.Object);

        GameThread.Run(() => broker.Publish(this, new AgentStationUseChanged(crew.Agent, crew.Point, inUse: false)), blocking: true);

        Assert.Equal(0, (long)JObject.FromObject(replicator.Inspect())["unownedPuppetReleases"]!);
        network.Verify(n => n.SendAll(It.IsAny<NetworkAgentStationUse>()), Times.Never);
    }

    // R50 F2: the owner's release for a puppet vanilla already let go here has nothing left to release.
    [Fact]
    public void Tick_SettlesTheOwnersReleaseOfAPuppetVanillaAlreadyReleased_AsObsolete()
    {
        var crew = new RemoteCrew();
        bool seated = true;
        crew.Engine.Setup(e => e.IsAlive(crew.Agent)).Returns(true);
        crew.Engine.Setup(e => e.IsSeated(crew.Agent, crew.Point)).Returns(() => seated);
        using var replicator = crew.CreateReplicator();
        replicator.AcceptUse(Use(crew.AgentId, crew.ShipId, inUse: true, revision: 1, "owner"));
        replicator.Tick(0f);

        seated = false;
        replicator.AcceptUse(Use(crew.AgentId, crew.ShipId, inUse: false, revision: 2, "owner"));
        replicator.Tick(0f);

        var inspected = JObject.FromObject(replicator.Inspect());
        Assert.Equal(1, (long)inspected["obsoleteReleases"]!);
        Assert.Equal(0, (long)inspected["ownerReleases"]!);
        Assert.Equal(0, (long)inspected["unownedPuppetReleases"]!);
        Assert.Equal(0, (int)inspected["appliedSeats"]!);
        crew.Engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), false), Times.Never);
    }

    // R50 F2: a seat vanilla took from a puppet whose owner still keeps it seated is the desync the counter reports.
    [Fact]
    public void Tick_CountsAnUnownedReleaseOnlyWhenTheOwnerStillKeepsTheSeat()
    {
        var crew = new RemoteCrew();
        crew.Engine.Setup(e => e.IsAlive(crew.Agent)).Returns(true);
        StationOf(crew.Engine, crew.Point, crew.Hull);
        using var replicator = crew.CreateReplicator();
        replicator.AcceptUse(Use(crew.AgentId, crew.ShipId, inUse: true, revision: 1, "owner"));

        replicator.Tick(0f);
        replicator.Tick(0f);

        Assert.Equal(1, (long)JObject.FromObject(replicator.Inspect())["unownedPuppetReleases"]!);
    }

    // S12 A5: a puppet adopted in place while seated keeps its seat as this client's own, announced under this client's stamp.
    [Fact]
    public void RefreshAppliedSeats_MakesAPuppetAdoptedWhileSeatedAnOwnSeat()
    {
        var crew = new RemoteCrew();
        crew.Registry.Setup(r => r.IsLocallyControlled(crew.Agent)).Returns(() => crew.Info.CurrentAuthority == "local");
        crew.Registry.Setup(r => r.IsLocallyControlled(crew.AgentId)).Returns(() => crew.Info.CurrentAuthority == "local");
        StationOf(crew.Engine, crew.Point, crew.Hull);
        crew.Engine.Setup(e => e.IsAlive(crew.Agent)).Returns(true);
        crew.Engine.Setup(e => e.IsSeated(crew.Agent, crew.Point)).Returns(true);
        var session = new Mock<IBattleSession>();
        session.SetupGet(s => s.OwnControllerId).Returns("local");
        var deployment = new Mock<IBattleDeploymentCoordinator>();
        deployment.SetupGet(d => d.IsCommitted).Returns(true);
        var network = new Mock<IBattleNetwork>();
        using var replicator = CreateReplicator(crew.Engine.Object, crew.Registry.Object, crew.Ships, network: network.Object,
            deployment: deployment.Object, session: session.Object);
        replicator.AcceptUse(Use(crew.AgentId, crew.ShipId, inUse: true, revision: 4, "owner"));
        replicator.Tick(0f);
        bool ownBeforeAdoption = replicator.IsOwnSeat(crew.AgentId);

        crew.Info.CurrentAuthority = "local";
        replicator.Tick(0f);

        var inspected = JObject.FromObject(replicator.Inspect());
        Assert.False(ownBeforeAdoption);
        Assert.True(replicator.IsOwnSeat(crew.AgentId));
        Assert.Equal(0, (int)inspected["appliedSeats"]!);
        Assert.Equal(1, (long)inspected["adoptedSeats"]!);
        network.Verify(n => n.SendAll(It.Is<NetworkAgentStationUse>(use =>
            use.AgentId == crew.AgentId && use.InUse && use.Revision == 1 && use.SenderControllerId == "local")), Times.Once);
    }

    // A puppet of a remote owner, crewing a registered hull whose oar station resolves.
    private sealed class RemoteCrew
    {
        public RemoteCrew()
        {
            Agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
            var info = Info = new CoopAgentInfo("owner", "owner", "battle", Agent, AgentId, 1);
            Registry.Setup(r => r.TryGetAgentInfo(AgentId, out info)).Returns(() => Registered);
            Registry.Setup(r => r.TryGetAgentInfo(Agent, out info)).Returns(() => Registered);
            Ships.TryRegister(new NetworkShipInfo(ShipId, "owner", "party", isNpcParty: false, Hull, null!));
            Engine.Setup(e => e.ResolveStation(Hull, "oar", 0)).Returns(Point);
        }

        public Guid AgentId { get; } = Guid.NewGuid();
        public Guid ShipId { get; } = Guid.NewGuid();
        public Agent Agent { get; }
        public CoopAgentInfo Info { get; }
        public bool Registered { get; set; } = true;
        public Mock<INetworkAgentRegistry> Registry { get; } = new Mock<INetworkAgentRegistry>();
        public NetworkShipRegistry Ships { get; } = new NetworkShipRegistry();
        public MissionObject Hull { get; } = ShipTestHulls.Create();
        public UsableMissionObject Point { get; } = CreatePoint();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();

        public AgentStationUseReplicator CreateReplicator(MessageBroker? broker = null) =>
            AgentStationUseReplicatorTests.CreateReplicator(Engine.Object, Registry.Object, Ships, broker);
    }

    private static NetworkAgentStationUse Use(Guid agentId, Guid shipId, bool inUse, long revision, string sender) =>
        new NetworkAgentStationUse(agentId, shipId, "oar", 0, inUse, revision, sender);
}
