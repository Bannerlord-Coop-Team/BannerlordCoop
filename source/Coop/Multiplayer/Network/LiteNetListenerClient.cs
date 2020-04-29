using Coop.Common;
using Coop.Network;
using LiteNetLib;
using System.Net;
using System.Net.Sockets;

namespace Coop.Multiplayer.Network
{
    public class LiteNetListenerClient : INetEventListener
    {
        private readonly GameSession m_Session;
        public LiteNetListenerClient(GameSession session)
        {
            m_Session = session;
        }
        public void OnPeerConnected(NetPeer peer)
        {
            var network = new LiteNetConnection(peer);
            var persistence = new RailNetPeerWrapper(network);
            var connection = new ConnectionClient(network, persistence, m_Session.World);
            m_Session.ConnectionCreated(connection);
            peer.Tag = connection;
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
                m_Session.Disconnect(eReason);
            }
        }
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            if(reader.IsNull)
            {
                throw new InvalidNetworkPackageException($"Received empty package from ${peer}.");
            }
            peer.GetConnection().Receive(reader.GetRemainingBytesSegment());
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
