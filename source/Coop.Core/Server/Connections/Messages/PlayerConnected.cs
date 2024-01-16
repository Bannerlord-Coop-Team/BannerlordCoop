using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// A player has connected
/// </summary>
public record PlayerConnected : IEvent
{
    public NetPeer PlayerPeer { get; }

    public PlayerConnected(NetPeer peer)
    {
        PlayerPeer = peer;
    }
}
