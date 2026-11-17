using Common.Messaging;
using ProtoBuf;

namespace Missions.Sessions.Messages;

/// <summary>
/// [Client to server] This client finished loading the session mission and asks for the session host. The
/// server takes the controller from the sending peer, so the message carries no controller id.
/// </summary>
[ProtoContract]
public readonly struct NetworkRequestSessionHost : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;

    public NetworkRequestSessionHost(string sessionId)
    {
        SessionId = sessionId;
    }
}
