using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Messages
{
    public class TestOutboundMessage : INetworkPeerMessage
    {
        public NetPeer NetPeer { get; set; }
        public object Data { get; set; }
    }
}
