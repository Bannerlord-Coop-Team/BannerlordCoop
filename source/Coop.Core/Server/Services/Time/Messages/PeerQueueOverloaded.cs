using Common.Logging.Attributes;
using Common.Messaging;
using Common.Network;
using LiteNetLib;

namespace Coop.Core.Server.Services.Time.Messages;

/// <summary>
/// When a clients packet queue on the server exceeds <see cref="INetworkConfiguration.MaxPacketsInQueue"/>
/// </summary>
[DontLogMessage]
public record PeerQueueOverloaded : IEvent
{
    public NetPeer NetPeer { get; }
    public PeerQueueOverloaded(NetPeer netPeer)
    {
        NetPeer = netPeer;
    }
}
