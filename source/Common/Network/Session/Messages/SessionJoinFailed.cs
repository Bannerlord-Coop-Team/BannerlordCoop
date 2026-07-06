using Common.Messaging;

namespace Common.Network.Session.Messages;

/// <summary>
/// A discovery-initiated join could not produce usable join info.
/// </summary>
public record SessionJoinFailed : IEvent
{
    public string Reason { get; }

    public SessionJoinFailed(string reason)
    {
        Reason = reason;
    }
}
