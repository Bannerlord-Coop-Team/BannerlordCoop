using Coop.Network;
using LiteNetLib;
using System;

namespace Coop.Multiplayer.Network
{
    public class NetConnection : INetworkConnection
    {
        private readonly NetPeer m_Peer;
        public NetConnection(NetPeer peer)
        {
            m_Peer = peer;
        }
        public int FragmentLength => 50_000;
        public int MaxPackageLength => 50_000_000;

        public void SendRaw(byte[] raw)
        {
            m_Peer.Send(raw, DeliveryMethod.ReliableOrdered);
        }
        public void Close(EDisconnectReason eReason)
        {
            m_Peer.Flush();
            m_Peer.NetManager.DisconnectPeer(m_Peer, new byte[] { Convert.ToByte(EDisconnectReason.ServerIsFull) });
        }
        public override string ToString()
        {
            return $"{base.ToString()}-{m_Peer.EndPoint}-{m_Peer.ConnectionState}";
        }
    }
}
