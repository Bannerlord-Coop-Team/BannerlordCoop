using Common.Messaging;
using ProtoBuf;

namespace Common.Network.Messages;

/// <summary>
/// Carries the server's current connected-player count to clients.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkConnectedPlayersChanged : IEvent
{
    [ProtoMember(1)]
    public readonly int ConnectedPlayers;

    public NetworkConnectedPlayersChanged(int connectedPlayers)
    {
        ConnectedPlayers = connectedPlayers;
    }
}
