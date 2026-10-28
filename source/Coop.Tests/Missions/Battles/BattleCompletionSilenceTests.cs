#if DEBUG
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Agents;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks a silent member cannot report or depart, and can resume reporting without losing its result.</summary>
public class BattleCompletionSilenceTests
{
    /// <summary>Silence does not consume the result or epoch deduplication latch.</summary>
    [Fact]
    public void WithheldResult_ReportsExactlyOnceWhenSilenceIsDisabled()
    {
        var session = new Mock<IBattleSession>();
        session.SetupAllProperties();
        session.SetupGet(value => value.HasInstance).Returns(true);
        session.SetupGet(value => value.InstanceId).Returns("battle");
        session.SetupGet(value => value.HostEpoch).Returns(1);
        session.Object.DebugWithholdCompletion = true;
        var relay = new Mock<INetwork>();
        var mesh = new Mock<IBattleNetwork>();
        var broker = new Mock<IMessageBroker>();
        var committer = new BattleResultCommitter(mesh.Object, relay.Object, session.Object, broker.Object);

        committer.ReportResolvedResult(new MissionResult(BattleState.AttackerVictory, true, false, false));
        committer.ReportAcceptedResult();
        relay.Verify(value => value.SendAll(It.IsAny<IMessage>()), Times.Never);
        broker.Verify(value => value.Publish(It.IsAny<object>(), It.IsAny<BattleResultReporting>()), Times.Never);
        Assert.True(committer.TryGetResolvedState(out var result));
        Assert.Equal(BattleState.AttackerVictory, result);

        session.Object.DebugWithholdCompletion = false;
        committer.ReportAcceptedResult();
        committer.ReportAcceptedResult();
        relay.Verify(value => value.SendAll(It.IsAny<IMessage>()), Times.Once);
        broker.Verify(value => value.Publish(It.IsAny<object>(), It.IsAny<BattleResultReporting>()), Times.Once);
        mesh.Verify(value => value.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    /// <summary>Both retreat callbacks and mission teardown keep server membership while the relay remains connected.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SilentDeparture_SendsNoCompletionOrLeaveAndDoesNotStopRelay(bool wasRetreat)
    {
        var session = new Mock<IBattleSession>();
        session.SetupGet(value => value.DebugWithholdCompletion).Returns(true);
        session.SetupGet(value => value.HasInstance).Returns(true);
        session.SetupGet(value => value.InstanceId).Returns("battle");
        var relay = new Mock<INetwork>(MockBehavior.Strict);
        var broker = new MessageBroker();
        var component = new Mock<ICoopMissionComponent> { DefaultValue = DefaultValue.Mock };
        var logic = new BattleResultReadyLogic(Mock.Of<IBattleResultCommitter>(),
            Mock.Of<ISiegeEngineStateReporter>(), broker, session.Object,
            Mock.Of<IBattleDeploymentCoordinator>(), relay.Object);
        using var lifecycle = new BattleInstanceLifecycle(Mock.Of<IBattleNetwork>(), relay.Object, broker,
            Mock.Of<IObjectManager>(), component.Object, Mock.Of<INetworkWorldItemRegistry>(),
            session.Object, Mock.Of<IMissionContext>());

        logic.OnRetreatMission();
        lifecycle.Leave(wasRetreat);

        relay.VerifyNoOtherCalls();
    }
}
#endif
