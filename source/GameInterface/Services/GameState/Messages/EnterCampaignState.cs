using Common.Messaging;

namespace GameInterface.Services.GameState.Messages
{ 
    /// <summary>
    /// Goes to the map state from any game state.
    /// </summary>
    public readonly struct EnterCampaignState : ICommand
    {
    }

    /// <summary>
    /// Reply to <seealso cref="EnterMainMenu"/>.
    /// </summary>
    public readonly struct CampaignStateEntered : ICommand
    {
    }
}
