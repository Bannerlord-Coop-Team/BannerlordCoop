using Common.Messaging;
using LiteNetLib;

namespace Common.Network.Messages;

/// <summary>
/// A player has disconnected. Lives in Common so both the networking layer (which raises it) and
/// GameInterface server-side handlers (which release per-peer state) can reference it.
/// </summary>
public record PlayerDisconnected : IEvent
{
    public PlayerDisconnected(NetPeer playerId, DisconnectInfo disconnectInfo)
    {
        PlayerId = playerId;
        DisconnectInfo = disconnectInfo;
    }

    public NetPeer PlayerId { get; }
    public DisconnectInfo DisconnectInfo { get; }
}
