using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// A player has disconnected
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
