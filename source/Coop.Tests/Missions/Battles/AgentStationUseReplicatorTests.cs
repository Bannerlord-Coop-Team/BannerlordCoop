using Common.Messaging;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.Serialization;
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

    // R7: a use its owner sent before leaving can arrive after the departure; it is dropped, not retried.
    [Fact]
    public void TryApply_DropsAUseFromASenderNoLongerInTheMission()
    {
        var engine = new Mock<INavalShipEngine>();
        using var replicator = CreateReplicator(engine.Object, members: Members("someone_else"));

        bool settled = replicator.TryApply(Use(Guid.NewGuid(), Guid.NewGuid(), inUse: true, revision: 1, "owner"));

        Assert.True(settled);
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);
        Assert.Equal(1, (long)JObject.FromObject(replicator.Inspect())["dropped"]!);
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
        ships.MarkAwaiting(shipId);
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
        IMissionContext? members = null)
    {
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());
        component.SetupGet(c => c.AgentActionHandler).Returns(Mock.Of<IAgentActionHandler>());
        component.SetupGet(c => c.AgentRegistry).Returns(registry ?? RemoteOwnerRegistry());
        component.SetupGet(c => c.ShipRegistry).Returns(ships ?? Mock.Of<INetworkShipRegistry>());
        return new AgentStationUseReplicator(Mock.Of<IBattleNetwork>(), broker ?? new MessageBroker(), Mock.Of<IBattleSession>(),
            component.Object, engine, Mock.Of<IBattleDeploymentCoordinator>(), members ?? Members("owner", "old", "new"));
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
        engine.Setup(e => e.TryDescribeStation(point, out stationHull, out stationKey, out pointIndex)).Returns(true);
        var broker = new MessageBroker();
        using var replicator = CreateReplicator(engine.Object, registry.Object, ships.Object, broker);

        broker.Publish(this, new AgentStationUseChanged(agent, point, inUse: true));
        replicator.Tick(0.1f);
        bool seatedWhileOwned = replicator.IsOwnSeat(agentId);
        locallyControlled = false;
        replicator.Tick(0.1f);

        Assert.True(seatedWhileOwned);
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

    private static NetworkAgentStationUse Use(Guid agentId, Guid shipId, bool inUse, long revision, string sender) =>
        new NetworkAgentStationUse(agentId, shipId, "oar", 0, inUse, revision, sender);
}
