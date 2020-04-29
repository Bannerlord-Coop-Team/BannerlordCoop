using Coop.Network;
using LiteNetLib;
using System;

namespace Coop.Multiplayer.Network
{
    public class LiteNetConnection : INetworkConnection
    {
        private readonly NetPeer m_Peer;
        public LiteNetConnection(NetPeer peer)
        {
            m_Peer = peer;
        }
        public int FragmentLength => 100_000;
        public int MaxPackageLength => 100_000_000;

        public int Latency => m_Peer.Ping;

        public void SendRaw(ArraySegment<byte> raw)
        {
            m_Peer.Send(raw.Array, raw.Offset, raw.Count, DeliveryMethod.ReliableOrdered);
        }
        public void Close(EDisconnectReason eReason)
        {
            m_Peer.Flush();
            m_Peer.NetManager.DisconnectPeer(m_Peer, new byte[] { Convert.ToByte(EDisconnectReason.ServerIsFull) });
        }
        public override string ToString()
        {
            return $"{base.ToString()}-{m_Peer.EndPoint.ToFriendlyString()}-{m_Peer.ConnectionState}";
        }
    }
}
