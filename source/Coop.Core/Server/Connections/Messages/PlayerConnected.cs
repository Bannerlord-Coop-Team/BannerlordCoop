using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// A player has connected
/// </summary>
public record PlayerConnected : IEvent
{
    public NetPeer PlayerId { get; }

    public PlayerConnected(NetPeer playerId)
    {
        PlayerId = playerId;
    }
}
