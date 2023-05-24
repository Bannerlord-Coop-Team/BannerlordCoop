using Common.Messaging;
using System;

namespace GameInterface.Services.GameState.Messages;

/// <summary>
/// Goes to the map state from any game state.
/// </summary>
public record EnterCampaignState : ICommand
{
}

/// <summary>
/// Campaign map entered event
/// </summary>
public record CampaignStateEntered : IEvent
{
}
