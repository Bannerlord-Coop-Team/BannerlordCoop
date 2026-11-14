using LiteNetLib;
using LiteNetLib.Utils;

namespace Coop.Core.Server.Connections;

/// <summary>Disconnects a peer with a reason code its client shows: a failed join, a shutdown or an unresponsive battle host.</summary>
public interface IJoinPeerTerminator
{
    void Disconnect(NetPeer peer, string reason);
}

/// <inheritdoc cref="IJoinPeerTerminator"/>
internal sealed class JoinPeerTerminator : IJoinPeerTerminator
{
    public void Disconnect(NetPeer peer, string reason)
    {
        var data = new NetDataWriter();
        data.Put(reason);
        peer.Disconnect(data);
    }
}
