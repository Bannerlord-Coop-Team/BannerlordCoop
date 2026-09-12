using Common.Messaging;

namespace Common.Network.Messages;

/// <summary>
/// Signals that the connected-player set used by server-side campaign policies changed.
/// </summary>
public readonly struct PlayerConnectionStateChanged : IEvent
{
}
