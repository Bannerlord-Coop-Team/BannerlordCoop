using Common.Messaging;
using System;

namespace GameInterface.Services.GameState.Messages;

/// <summary>
/// Goes to the main menu from any game state.
/// </summary>
public record EnterMainMenu : ICommand
{
}

/// <summary>
/// Response to <see cref="EnterMainMenu"/> publisher
/// </summary>
public record EnterMainMenuResponse : IResponse
{
}

/// <summary>
/// Event when main menu is entered
/// </summary>
public record MainMenuEntered : IEvent
{
}
