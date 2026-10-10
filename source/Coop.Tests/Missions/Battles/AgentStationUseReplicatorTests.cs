using Common.Messaging;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Moq;
using System;
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

    private static AgentStationUseReplicator CreateReplicator(INavalShipEngine engine)
    {
        var component = new Mock<ICoopMissionComponent>();
        component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());
        component.SetupGet(c => c.AgentActionHandler).Returns(Mock.Of<IAgentActionHandler>());
        component.SetupGet(c => c.AgentRegistry).Returns(Mock.Of<INetworkAgentRegistry>());
        return new AgentStationUseReplicator(Mock.Of<IBattleNetwork>(), new MessageBroker(), Mock.Of<IBattleSession>(),
            component.Object, engine, Mock.Of<IBattleDeploymentCoordinator>());
    }

    // A distinct point identity; the engine mock never touches it.
    private static UsableMissionObject CreatePoint() => ShipTestHulls.CreatePoint();

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(3, 4, true)]
    [InlineData(3, 3, false)]
    [InlineData(3, 2, false)]
    public void IsNewer_AcceptsOnlyRevisionsAfterTheLastApplied(long applied, long received, bool expected)
    {
        Assert.Equal(expected, AgentStationUseReplicator.IsNewer(applied, received));
    }
}
