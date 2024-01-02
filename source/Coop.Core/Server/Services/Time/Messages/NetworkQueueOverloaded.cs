using Common.Messaging;
using Common.Network;
using LiteNetLib;

namespace Coop.Core.Server.Services.Time.Messages;

/// <summary>
/// When a clients packet queue on the server exceeds <see cref="INetworkConfiguration.MaxPacketsInQueue"/>
/// </summary>
public record NetworkQueueOverloaded : IEvent
{
    public NetPeer NetPeer { get; }
    public NetworkQueueOverloaded(NetPeer netPeer)
    {
        NetPeer = netPeer;
    }
}
