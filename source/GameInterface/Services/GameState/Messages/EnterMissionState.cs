using Common.Messaging;
using System;

namespace GameInterface.Services.GameState.Messages;

/// <summary>
/// Goes to the mission state from any game state.
/// </summary>
public record EnterMissionState : ICommand
{
}

/// <summary>
/// Mission state entered event
/// </summary>
public record MissionStateEntered : IEvent
{
}
