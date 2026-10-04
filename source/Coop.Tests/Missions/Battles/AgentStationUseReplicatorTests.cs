using Common.Messaging;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Naval;
using Moq;
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

        using var replicator = new AgentStationUseReplicator(Mock.Of<IBattleNetwork>(), new MessageBroker(),
            Mock.Of<IBattleSession>(), component.Object, engine.Object, Mock.Of<IBattleDeploymentCoordinator>());
        replicator.Apply(null!, null!, inUse: true);

        Assert.Equal(new[] { "forget", "use" }, calls);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(3, 4, true)]
    [InlineData(3, 3, false)]
    [InlineData(3, 2, false)]
    public void IsNewer_AcceptsOnlyRevisionsAfterTheLastApplied(long applied, long received, bool expected)
    {
        Assert.Equal(expected, AgentStationUseReplicator.IsNewer(applied, received));
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void HelmGate_StopsOnlyThePlayerTakingAForeignHelm(bool isMainAgent, bool isForeignHelm, bool expected)
    {
        Assert.Equal(expected, ForeignHelmUsePatch.AllowsStart(isMainAgent, isForeignHelm));
    }
}
