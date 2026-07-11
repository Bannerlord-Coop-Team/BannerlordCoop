using GameInterface.Services.Tournaments;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments;

public class TournamentCompletionTransactionTests
{
    [Fact]
    public void RewardCompletion_DoesNotFinalizeUntilNativeAndSessionRemovalRun()
    {
        var transaction = new TournamentCompletionTransaction();

        transaction.Run(TournamentCompletionStep.Leaderboard, () => { });
        transaction.Run(TournamentCompletionStep.Influence, () => { });
        transaction.Run(TournamentCompletionStep.Prize, () => { });
        transaction.Run(TournamentCompletionStep.BetPayout, () => { });
        transaction.Run(TournamentCompletionStep.BetSettlement, () => { });
        transaction.Run(TournamentCompletionStep.SimulationProgression, () => { });
        Assert.True(transaction.IsReadyForRemoval);
        Assert.False(transaction.IsCompleted);

        transaction.Run(TournamentCompletionStep.FinishedEvent, () => { });
        transaction.Run(TournamentCompletionStep.NativeRemoval, () => { });
        transaction.Run(TournamentCompletionStep.SessionRemoval, () => { });

        Assert.True(transaction.IsCompleted);
    }
}