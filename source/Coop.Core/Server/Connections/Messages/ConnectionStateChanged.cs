using Common.Messaging;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Published by a <see cref="ConnectionLogic"/> after it has transitioned to a new
/// <see cref="IConnectionState"/>. The <see cref="ClientRegistry"/> listens for this to
/// recompute aggregate connection state (such as how many players are loading) from a single,
/// correctly-timed point: after the new state has been assigned.
/// </summary>
internal record ConnectionStateChanged : IEvent
{
}
