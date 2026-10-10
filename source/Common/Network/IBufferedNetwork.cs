using LiteNetLib;

namespace Common.Network;

/// <summary>Optional cleanup for transports that aggregate unsent messages per peer.</summary>
public interface IBufferedNetwork
{
    void DiscardPendingMessages(NetPeer peer);
}
