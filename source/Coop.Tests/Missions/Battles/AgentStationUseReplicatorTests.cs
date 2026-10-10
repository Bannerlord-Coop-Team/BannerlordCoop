using Common.Messaging;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Messages;
using Moq;
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
            Mock.Of<IBattleSession>(), component.Object, engine.Object, Mock.Of<IBattleDeploymentCoordinator>());
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
        using var replicator = CreateReplicator(engine.Object);
        var agentId = Guid.NewGuid();
        var point = CreatePoint();
        replicator.RecordAppliedSeat(agentId, null!, null!, point);

        replicator.RefreshAppliedSeats();
        replicator.RefreshAppliedSeats();
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), point, true), Times.Once);

        replicator.RecordAppliedSeat(agentId, null!, null!, point);
        replicator.RefreshAppliedSeats();
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), point, true), Times.Exactly(2));
        engine.Verify(e => e.PinToStation(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>()), Times.Never);
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

        // Vanilla Agent.OnRemove already stops a removed agent using its station.
        engine.Verify(e => e.ApplyStationUse(It.IsAny<Agent>(), It.IsAny<UsableMissionObject>(), It.IsAny<bool>()), Times.Never);
    }

    private static AgentStationUseReplicator CreateReplicator(INavalShipEngine engine,
        INetworkAgentRegistry? registry = null, INetworkShipRegistry? ships = null, MessageBroker? broker = null)
    {
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());
        component.SetupGet(c => c.AgentActionHandler).Returns(Mock.Of<IAgentActionHandler>());
        component.SetupGet(c => c.AgentRegistry).Returns(registry ?? Mock.Of<INetworkAgentRegistry>());
        component.SetupGet(c => c.ShipRegistry).Returns(ships ?? Mock.Of<INetworkShipRegistry>());
        return new AgentStationUseReplicator(Mock.Of<IBattleNetwork>(), broker ?? new MessageBroker(), Mock.Of<IBattleSession>(),
            component.Object, engine, Mock.Of<IBattleDeploymentCoordinator>());
    }

    // A distinct point identity; the engine mock never touches it.
    private static UsableMissionObject CreatePoint() => ShipTestHulls.CreatePoint();

    [Theory]
    [InlineData(null, 0, "old", 1, true)]
    [InlineData("old", 3, "old", 4, true)]
    [InlineData("old", 3, "old", 3, false)]
    [InlineData("old", 3, "old", 2, false)]
    [InlineData("old", 3, "new", 1, true)]
    public void IsNewer_AcceptsRevisionsAfterTheLastAppliedOrFromANewSender(string? appliedSender, long applied,
        string sender, long received, bool expected)
    {
        Assert.Equal(expected, AgentStationUseReplicator.IsNewer(appliedSender!, applied, sender, received));
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

    private static NetworkAgentStationUse Use(Guid agentId, Guid shipId, bool inUse, long revision, string sender) =>
        new NetworkAgentStationUse(agentId, shipId, "oar", 0, inUse, revision, sender);
}
