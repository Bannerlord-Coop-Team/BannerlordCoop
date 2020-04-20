using Coop.Common;
using Coop.Multiplayer;
using Coop.Network;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Multiplayer.Network
{
    public class NetListenerClient : INetEventListener
    {
        private readonly ClientSession m_Session;
        public NetListenerClient(ClientSession session)
        {
            m_Session = session;
        }
        public void OnPeerConnected(NetPeer peer)
        {
            var conn = new ConnectionClient(new NetConnection(peer));
            m_Session.OnConnectionCreated(conn);
            peer.Tag = conn;
        }
        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            EDisconnectReason eReason = EDisconnectReason.Unknown;
            if(disconnectInfo.AdditionalData.AvailableBytes == 1)
            {
                eReason = (EDisconnectReason)disconnectInfo.AdditionalData.GetByte();
            }

            if(m_Session.Connection != null)
            {
                m_Session.OnDisconnect(eReason);
            }
        }
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            if(reader.IsNull)
            {
                throw new InvalidNetworkPackageException($"Received empty package from ${peer}.");
            }
            peer.GetConnection().Receive(new ByteReader(reader.GetRemainingBytes()));
        }
        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            Log.Error($"OnNetworkError({endPoint}, {socketError}).");
        }
        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            Log.Debug($"OnNetworkReceiveUnconnected({remoteEndPoint}, {reader}, {messageType}).");
        }

        public void OnConnectionRequest(ConnectionRequest request) { }
        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    }
}
