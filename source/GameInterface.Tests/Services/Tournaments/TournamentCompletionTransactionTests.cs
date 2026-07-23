using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Handlers;
using System.Collections.Generic;
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

        var transactions = new Dictionary<string, TournamentCompletionTransaction>
        {
            ["session-a"] = transaction,
            ["session-b"] = new TournamentCompletionTransaction()
        };
        Assert.False(TournamentSessionHandler.RemoveCompletedTransaction(
            transactions,
            "session-a",
            transaction));
        Assert.Contains("session-a", transactions);

        transaction.Run(TournamentCompletionStep.FinishedEvent, () => { });
        transaction.Run(TournamentCompletionStep.NativeRemoval, () => { });
        transaction.Run(TournamentCompletionStep.SessionRemoval, () => { });

        Assert.True(transaction.IsCompleted);
        Assert.True(TournamentSessionHandler.RemoveCompletedTransaction(
            transactions,
            "session-a",
            transaction));
        Assert.DoesNotContain("session-a", transactions);
        Assert.Contains("session-b", transactions);
    }
}
