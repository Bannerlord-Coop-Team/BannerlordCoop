using Common.Messaging;

namespace GameInterface.Services.Players.Messages;

/// <summary>
/// Client-local request (from the coop.delete_player command) to have the server delete this
/// player's hero and disconnect the client. Forwarded to the server as
/// <see cref="NetworkRequestDeletePlayer"/>.
/// </summary>
public record PlayerDeleteRequested : IEvent
{
}
