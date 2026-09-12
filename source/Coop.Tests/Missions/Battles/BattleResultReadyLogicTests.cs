using Common;
using Common.Messaging;
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
            Mock.Of<IBattleDeploymentCoordinator>());
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
            Mock.Of<IBattleDeploymentCoordinator>());
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
            deployment.Object);

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
            deployment.Object);

        messageBroker.Publish(this, new BattleHostAuthorityAcquired("battle"));
        GameThread.Run(() => { }, blocking: true);

        siegeReporter.VerifyNoOtherCalls();
        resultCommitter.VerifyNoOtherCalls();
    }
}
