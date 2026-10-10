using Common;
using Common.Messaging;
using Common.Network;
using Missions.Battles;
using Missions.Messages;
using Moq;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleResultReadyLogicTests
{
    [Fact]
    public void ResolvedResult_ReportsSiegeStateBeforeBattleResult()
    {
        var result = new MissionResult(
            BattleState.AttackerVictory,
            playerVictory: true,
            playerDefeated: false,
            enemyRetreated: false);
        var sequence = new MockSequence();
        var resultCommitter = new Mock<IBattleResultCommitter>(MockBehavior.Strict);
        var siegeReporter = new Mock<ISiegeEngineStateReporter>(MockBehavior.Strict);
        siegeReporter.InSequence(sequence)
            .Setup(reporter => reporter.ReportConcludedIfHost());
        resultCommitter.InSequence(sequence)
            .Setup(committer => committer.ReportResolvedResult(result));

        var logic = new BattleResultReadyLogic(
            resultCommitter.Object,
            siegeReporter.Object,
            new MessageBroker(),
            Mock.Of<IBattleSession>(),
            Mock.Of<IBattleDeploymentCoordinator>(),
            Mock.Of<INetwork>());
        logic.OnMissionResultReady(result);

        siegeReporter.VerifyAll();
        resultCommitter.VerifyAll();
    }

    [Fact]
    public void UnresolvedResult_DoesNotReportConcludedSiegeState()
    {
        var result = new MissionResult();
        var resultCommitter = new Mock<IBattleResultCommitter>(MockBehavior.Strict);
        var siegeReporter = new Mock<ISiegeEngineStateReporter>(MockBehavior.Strict);
        resultCommitter
            .Setup(committer => committer.ReportResolvedResult(result));

        var logic = new BattleResultReadyLogic(
            resultCommitter.Object,
            siegeReporter.Object,
            new MessageBroker(),
            Mock.Of<IBattleSession>(),
            Mock.Of<IBattleDeploymentCoordinator>(),
            Mock.Of<INetwork>());
        logic.OnMissionResultReady(result);

        siegeReporter.VerifyNoOtherCalls();
        resultCommitter.VerifyAll();
    }

    [Fact]
    public void PromotedAfterResolution_ReportsSiegeStateBeforeCurrentEpochResult()
    {
        var result = new MissionResult(
            BattleState.DefenderVictory,
            playerVictory: false,
            playerDefeated: true,
            enemyRetreated: false);
        var messageBroker = new MessageBroker();
        var session = new Mock<IBattleSession>(MockBehavior.Strict);
        session.SetupGet(value => value.InstanceId).Returns("battle");
        session.SetupGet(value => value.IsLocalHost).Returns(true);
        var sequence = new MockSequence();
        var resultCommitter = new Mock<IBattleResultCommitter>(MockBehavior.Strict);
        var siegeReporter = new Mock<ISiegeEngineStateReporter>(MockBehavior.Strict);
        siegeReporter.InSequence(sequence).Setup(reporter => reporter.ReportConcludedIfHost());
        resultCommitter.InSequence(sequence).Setup(committer => committer.ReportResolvedResult(result));
        siegeReporter.InSequence(sequence).Setup(reporter => reporter.ReportConcludedIfHost());
        resultCommitter.InSequence(sequence).Setup(committer => committer.TryGetResolvedState(out It.Ref<BattleState>.IsAny))
            .Returns(true);
        resultCommitter.InSequence(sequence).Setup(committer => committer.ReportAcceptedResult());
        var deployment = new Mock<IBattleDeploymentCoordinator>(MockBehavior.Strict);
        deployment.SetupGet(value => value.IsCommitted).Returns(true);
        var logic = new BattleResultReadyLogic(
            resultCommitter.Object,
            siegeReporter.Object,
            messageBroker,
            session.Object,
            deployment.Object,
            Mock.Of<INetwork>());

        logic.OnMissionResultReady(result);
        messageBroker.Publish(this, new BattleHostAuthorityAcquired("battle"));
        GameThread.Run(() => { }, blocking: true);

        siegeReporter.VerifyAll();
        resultCommitter.VerifyAll();
    }

    [Fact]
    public void PromotedBeforeDeployment_DoesNotReportAcceptedResult()
    {
        var messageBroker = new MessageBroker();
        var session = new Mock<IBattleSession>(MockBehavior.Strict);
        session.SetupGet(value => value.InstanceId).Returns("battle");
        session.SetupGet(value => value.IsLocalHost).Returns(true);
        var deployment = new Mock<IBattleDeploymentCoordinator>(MockBehavior.Strict);
        deployment.SetupGet(value => value.IsCommitted).Returns(false);
        var resultCommitter = new Mock<IBattleResultCommitter>(MockBehavior.Strict);
        var siegeReporter = new Mock<ISiegeEngineStateReporter>(MockBehavior.Strict);
        _ = new BattleResultReadyLogic(
            resultCommitter.Object,
            siegeReporter.Object,
            messageBroker,
            session.Object,
            deployment.Object,
            Mock.Of<INetwork>());

        messageBroker.Publish(this, new BattleHostAuthorityAcquired("battle"));
        GameThread.Run(() => { }, blocking: true);

        siegeReporter.VerifyNoOtherCalls();
        resultCommitter.VerifyNoOtherCalls();
    }

    [Fact]
    public void UnresolvedRetreatMission_ReportsCurrentBattleRetreat()
    {
        var session = new Mock<IBattleSession>(MockBehavior.Strict);
        session.SetupGet(value => value.HasInstance).Returns(true);
        session.SetupGet(value => value.InstanceId).Returns("battle");
        
        var relayNetwork = new Mock<INetwork>(MockBehavior.Strict);
        relayNetwork.Setup(network => network.SendAll(It.Is<NetworkBattleRetreated>(message => message.InstanceId == "battle")));
        
        var logic = new BattleResultReadyLogic(
            Mock.Of<IBattleResultCommitter>(),
            Mock.Of<ISiegeEngineStateReporter>(),
            new MessageBroker(),
            session.Object,
            Mock.Of<IBattleDeploymentCoordinator>(),
            relayNetwork.Object);

        logic.OnRetreatMission();

        relayNetwork.VerifyAll();
    }

    [Fact]
    public void ResolvedResultFollowedByRetreatCallback_DoesNotReportRetreat()
    {
        var result = new MissionResult(BattleState.AttackerVictory, playerVictory: true, playerDefeated: false,
            enemyRetreated: false);
        
        var resultCommitter = new Mock<IBattleResultCommitter>(MockBehavior.Strict);
        resultCommitter.Setup(committer => committer.ReportResolvedResult(result));
        
        var siegeReporter = new Mock<ISiegeEngineStateReporter>(MockBehavior.Strict);
        siegeReporter.Setup(reporter => reporter.ReportConcludedIfHost());
        
        var relayNetwork = new Mock<INetwork>(MockBehavior.Strict);
        
        var logic = new BattleResultReadyLogic(resultCommitter.Object, siegeReporter.Object, new MessageBroker(), Mock.Of<IBattleSession>(), Mock.Of<IBattleDeploymentCoordinator>(), relayNetwork.Object);
        
        logic.OnMissionResultReady(result);
        logic.OnRetreatMission();
        
        resultCommitter.VerifyAll();
        siegeReporter.VerifyAll();
        relayNetwork.VerifyNoOtherCalls();
    }
}
