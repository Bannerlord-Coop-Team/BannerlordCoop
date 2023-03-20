using Common.Messaging;

namespace GameInterface.Services.GameState.Messages
{
    /// <summary>
    /// Goes to the mission state from any game state.
    /// </summary>
    public readonly struct EnterMissionState : ICommand
    {
    }

    /// <summary>
    /// Reply to <seealso cref="EnterMainMenu"/>.
    /// </summary>
    public readonly struct MissionStateEntered : ICommand
    {
    }
}
