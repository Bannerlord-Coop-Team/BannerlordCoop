using Common.Messaging;

namespace Coop.Core.Common.Services.Connection.Messages;

/// <summary>
/// Event signifying that the Coop Mode (module) should end
/// </summary>
public record EndCoopMode : ICommand
{
}

/// <summary>
/// Event signifying that the Coop Mode (module) has been disposed
/// </summary>
public record CoopModeEnded : IEvent
{
}