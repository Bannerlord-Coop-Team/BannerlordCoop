using Common.Logging.Attributes;
using Common.Messaging;
using Common.Network;
using LiteNetLib;

namespace Coop.Core.Server.Services.Time.Messages;

/// <summary>
/// When a client's packet queue on the server exceeds <see cref="INetworkConfiguration.SlowDownPacketThreshold"/>
/// but has not yet reached <see cref="INetworkConfiguration.MaxPacketsInQueue"/>. This is the moderate tier:
/// the client is falling behind, so server time is capped at 1x to let it catch up without a full pause.
/// </summary>
[DontLogMessage]
public record PeerQueueCongested : IEvent
{
    public NetPeer NetPeer { get; }
    public PeerQueueCongested(NetPeer netPeer)
    {
        NetPeer = netPeer;
    }
}
