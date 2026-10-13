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
        var committer = new BattleResultCommitter(Mock.Of<IBattleNetwork>(), relay.Object, session.Object);

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
        var committer = new BattleResultCommitter(Mock.Of<IBattleNetwork>(), relay.Object, session.Object);

        committer.AcceptResolvedState(BattleState.AttackerVictory);
        committer.ReportAcceptedResult();
        hasInstance = true;
        committer.ReportAcceptedResult();

        relay.Verify(r => r.SendAll(It.IsAny<NetworkBattleResultReady>()), Times.Once);
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
