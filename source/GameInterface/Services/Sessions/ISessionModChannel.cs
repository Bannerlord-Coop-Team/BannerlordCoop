using Common.Messaging;

namespace GameInterface.Services.Sessions;

/// <summary>
/// Session-scoped message channel for a mod: its own <c>[ProtoContract]</c> messages travel to the other members of
/// the live session mission, reliably and in order, and arrive on the message broker like any mission message.
/// Put the session id in each message so a stale one can be dropped.
/// </summary>
public interface ISessionModChannel
{
    /// <summary>Sends to every other member of the live session mission. False when no session mission is live.</summary>
    bool SendToSession(IMessage message);

    /// <summary>Sends to one member. False when no session mission is live.</summary>
    bool SendToController(string controllerId, IMessage message);
}
