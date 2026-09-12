using Common.Messaging;

namespace Common.Network.Session.Messages;

/// <summary>
/// The user abandoned a discovery-initiated join before it produced a session
/// (e.g. canceled the server password prompt).
/// </summary>
public record SessionJoinAbandoned : IEvent
{
}
