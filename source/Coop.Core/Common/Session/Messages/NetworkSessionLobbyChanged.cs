using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Common.Session.Messages;

/// <summary>
/// Identifies the Steam lobby owned by the authoritative server.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSessionLobbyChanged : IEvent
{
    [ProtoMember(1)]
    public readonly ulong LobbyId;

    public NetworkSessionLobbyChanged(ulong lobbyId)
    {
        LobbyId = lobbyId;
    }
}
