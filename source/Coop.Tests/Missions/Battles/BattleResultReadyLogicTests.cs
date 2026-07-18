using Missions.Battles;
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

        var logic = new BattleResultReadyLogic(resultCommitter.Object, siegeReporter.Object);
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

        var logic = new BattleResultReadyLogic(resultCommitter.Object, siegeReporter.Object);
        logic.OnMissionResultReady(result);

        siegeReporter.VerifyNoOtherCalls();
        resultCommitter.VerifyAll();
    }
}
