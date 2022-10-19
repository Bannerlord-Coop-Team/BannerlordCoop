using LiteNetLib;

namespace Common.Messaging
{
    public interface INetworkPeerMessage : INetworkMessage
    {
        NetPeer NetPeer { get; set; }
    }
}
