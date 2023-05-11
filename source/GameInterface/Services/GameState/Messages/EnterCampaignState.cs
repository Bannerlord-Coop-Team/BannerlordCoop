using Common.Messaging;
using System;

namespace GameInterface.Services.GameState.Messages;

/// <summary>
/// Goes to the map state from any game state.
/// </summary>
public readonly struct EnterCampaignState : ICommand
{
    public Guid TransactionID => throw new NotImplementedException();
}

/// <summary>
/// Reply to <seealso cref="EnterMainMenu"/>.
/// </summary>
public readonly struct CampaignStateEntered : IResponse
{
    public Guid TransactionID => throw new NotImplementedException();
}
