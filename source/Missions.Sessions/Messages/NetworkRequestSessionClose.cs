using Common.Messaging;
using ProtoBuf;

namespace Missions.Sessions.Messages;

/// <summary>
/// [Client to server] The session host asks the server to close the session. The server refuses it unless
/// the sender is the current host at <see cref="HostEpoch"/>.
/// </summary>
[ProtoContract]
public readonly struct NetworkRequestSessionClose : ICommand
{
    [ProtoMember(1)]
    public readonly string SessionId;

    [ProtoMember(2)]
    public readonly int HostEpoch;

    public NetworkRequestSessionClose(string sessionId, int hostEpoch)
    {
        SessionId = sessionId;
        HostEpoch = hostEpoch;
    }
}
