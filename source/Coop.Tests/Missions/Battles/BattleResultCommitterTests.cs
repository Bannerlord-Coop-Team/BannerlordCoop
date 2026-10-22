using Common.Messaging;
using Common.Network;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleResultCommitterTests
{
    [Fact]
    public void ReportAcceptedResult_SendsAStateOncePerHostEpoch()
    {
        int hostEpoch = 1;
        var session = new Mock<IBattleSession>();
        session.SetupGet(s => s.HasInstance).Returns(true);
        session.SetupGet(s => s.InstanceId).Returns("battle");
        session.SetupGet(s => s.HostEpoch).Returns(() => hostEpoch);
        var relay = new Mock<INetwork>();
        var committer = new BattleResultCommitter(Mock.Of<IBattleNetwork>(), relay.Object, session.Object, Mock.Of<IMessageBroker>());

        committer.ReportResolvedResult(new MissionResult(BattleState.DefenderVictory, true, false, false));
        committer.ReportAcceptedResult();
        committer.ReportResolvedResult(new MissionResult(BattleState.DefenderVictory, true, false, false));
        hostEpoch = 2;
        committer.ReportAcceptedResult();
        committer.ReportAcceptedResult();

        relay.Verify(r => r.SendAll(It.Is<NetworkBattleResultReady>(m => m.HostEpoch == 1)), Times.Once);
        relay.Verify(r => r.SendAll(It.Is<NetworkBattleResultReady>(m => m.HostEpoch == 2)), Times.Once);
        relay.Verify(r => r.SendAll(It.IsAny<IMessage>()), Times.Exactly(2));
    }

    [Fact]
    public void ReportAcceptedResult_RetriesAReportThatHadNoInstance()
    {
        bool hasInstance = false;
        var session = new Mock<IBattleSession>();
        session.SetupGet(s => s.HasInstance).Returns(() => hasInstance);
        session.SetupGet(s => s.InstanceId).Returns("battle");
        session.SetupGet(s => s.HostEpoch).Returns(1);
        var relay = new Mock<INetwork>();
        var committer = new BattleResultCommitter(Mock.Of<IBattleNetwork>(), relay.Object, session.Object, Mock.Of<IMessageBroker>());

        committer.AcceptResolvedState(BattleState.AttackerVictory);
        committer.ReportAcceptedResult();
        hasInstance = true;
        committer.ReportAcceptedResult();

        relay.Verify(r => r.SendAll(It.IsAny<NetworkBattleResultReady>()), Times.Once);
    }

    // M6: a battle type's own report (the naval ship fates) goes out when this event is published, ahead of the result.
    [Fact]
    public void ReportAcceptedResult_PublishesTheReportingEventBeforeTheResultGoesOut()
    {
        var order = new System.Collections.Generic.List<string>();
        var session = new Mock<IBattleSession>();
        session.SetupGet(s => s.HasInstance).Returns(true);
        session.SetupGet(s => s.InstanceId).Returns("battle");
        session.SetupGet(s => s.HostEpoch).Returns(1);
        var relay = new Mock<INetwork>();
        relay.Setup(r => r.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(message => order.Add(message.GetType().Name));
        var broker = new Mock<IMessageBroker>();
        broker.Setup(b => b.Publish(It.IsAny<object>(), It.IsAny<BattleResultReporting>()))
            .Callback<object, BattleResultReporting>((_, reporting) => order.Add(nameof(BattleResultReporting) + ":" + reporting.InstanceId));
        var committer = new BattleResultCommitter(Mock.Of<IBattleNetwork>(), relay.Object, session.Object, broker.Object);

        committer.AcceptResolvedState(BattleState.AttackerVictory);
        committer.ReportAcceptedResult();

        Assert.Equal(new[] { "BattleResultReporting:battle", nameof(NetworkBattleResultReady) }, order);
    }

    [Theory]
    [InlineData(BattleState.None, 0, BattleState.DefenderVictory, 0, false)]
    [InlineData(BattleState.DefenderVictory, 1, BattleState.DefenderVictory, 1, true)]
    [InlineData(BattleState.DefenderVictory, 1, BattleState.DefenderVictory, 2, false)]
    [InlineData(BattleState.DefenderVictory, 1, BattleState.AttackerVictory, 1, false)]
    public void IsAlreadyReported_MatchesTheSameStateAtTheSameEpoch(BattleState reported, int reportedEpoch,
        BattleState state, int epoch, bool expected)
    {
        Assert.Equal(expected, BattleResultCommitter.IsAlreadyReported(reported, reportedEpoch, state, epoch));
    }
}
