using Common.Messaging;

namespace GameInterface.Services.GameDebug.Messages;

/// <summary>
/// Shows all parties on the map
/// </summary>
/// <seealso cref="Handlers.ShowAllPartiesHandler"/>
public record ShowAllParties : ICommand
{
}
