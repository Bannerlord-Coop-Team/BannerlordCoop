using System;
using System.Collections.Generic;

namespace GameInterface.Services.Tournaments;

internal enum TournamentCompletionStep
{
    Leaderboard,
    Influence,
    Prize,
    BetPayout,
    BetSettlement,
    SimulationProgression,
    FinishedEvent,
    NativeRemoval,
    SessionRemoval
}

internal sealed class TournamentCompletionTransaction
{
    private readonly HashSet<TournamentCompletionStep> completedSteps = new();

    public bool IsCompleted => completedSteps.Contains(TournamentCompletionStep.SessionRemoval);
    public bool IsReadyForRemoval =>
        completedSteps.Contains(TournamentCompletionStep.Leaderboard) &&
        completedSteps.Contains(TournamentCompletionStep.Influence) &&
        completedSteps.Contains(TournamentCompletionStep.Prize) &&
        completedSteps.Contains(TournamentCompletionStep.BetPayout) &&
        completedSteps.Contains(TournamentCompletionStep.BetSettlement) &&
        completedSteps.Contains(TournamentCompletionStep.SimulationProgression);

    public void Run(TournamentCompletionStep step, Action action)
    {
        if (completedSteps.Contains(step))
            return;

        action();
        completedSteps.Add(step);
    }
}
