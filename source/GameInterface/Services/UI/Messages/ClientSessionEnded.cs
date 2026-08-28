using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

/// <summary>
/// Published on the game thread when a client session ends (disconnect, kicked, or manual leave).
/// Used by UI to reset in-flight state that is not cleared by join-failure or join-abandon paths.
/// </summary>
public record ClientSessionEnded : IEvent
{
}
