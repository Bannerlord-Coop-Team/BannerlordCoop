using LiteNetLib;

namespace Coop.Core.Server.Connections;

/// <summary>Disconnects a failed join after its replay has been made terminal.</summary>
public interface IJoinPeerTerminator
{
    void Disconnect(NetPeer peer, string reason);
}

/// <inheritdoc cref="IJoinPeerTerminator"/>
internal sealed class JoinPeerTerminator : IJoinPeerTerminator
{
    public void Disconnect(NetPeer peer, string reason) => peer.Disconnect();
}
