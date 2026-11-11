namespace Common.BattleBenchmark;

/// <summary>A game-thread hot path the battle benchmark times.</summary>
public enum BattleBenchmarkSection
{
    /// <summary>Capturing and sending owned agents' movement (<c>AgentMovementHandler.PollMovement</c>).</summary>
    MovementSend = 0,

    /// <summary>Applying received movement to remote agents.</summary>
    MovementApply = 1,

    /// <summary>Polling owned agents' actions after the native tick (<c>AgentActionHandler.PollActionsAfterNativeTick</c>).</summary>
    ActionPoll = 2,

    /// <summary>Running the actions queued on the <see cref="GameThread"/>.</summary>
    GameThreadDrain = 3,

    Count = 4,
}
