using System.Net;
using System.Net.Sockets;
using Coop.Network;
using LiteNetLib;
using NLog;

namespace Coop.Multiplayer.Network
{
    public class LiteNetListenerClient : INetEventListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly GameSession m_Session;

        public LiteNetListenerClient(GameSession session)
        {
            m_Session = session;
        }

        public void OnPeerConnected(NetPeer peer)
        {
            LiteNetConnection network = new LiteNetConnection(peer);
            RailNetPeerWrapper persistence = new RailNetPeerWrapper(network);
            ConnectionClient connection = new ConnectionClient(
                network,
                persistence,
                m_Session.World);
            m_Session.ConnectionCreated(connection);
            peer.Tag = connection;
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            EDisconnectReason eReason = EDisconnectReason.Unknown;
            if (disconnectInfo.AdditionalData.AvailableBytes == 1)
            {
                eReason = (EDisconnectReason) disconnectInfo.AdditionalData.GetByte();
            }
            else
            {
                disconnectInfo.GetReason(false);
            }

            if (m_Session.Connection != null)
            {
                m_Session.Disconnect(eReason);
            }
        }

        public void OnNetworkReceive(
            NetPeer peer,
            NetPacketReader reader,
            DeliveryMethod deliveryMethod)
        {
            if (reader.IsNull)
            {
                throw new InvalidNetworkPackageException($"Received empty package from ${peer}.");
            }

            peer.GetConnection().Receive(reader.GetRemainingBytesSegment());
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            Logger.Error(
                "OnNetworkError({endPoint}, {socketError}).",
                endPoint.ToFriendlyString(),
                socketError);
            if (m_Session.Connection != null)
            {
                m_Session.Disconnect(EDisconnectReason.Unknown);
            }
        }

        public void OnNetworkReceiveUnconnected(
            IPEndPoint remoteEndPoint,
            NetPacketReader reader,
            UnconnectedMessageType messageType)
        {
            Logger.Warn(
                "OnNetworkReceiveUnconnected({remoteEndPoint}, {reader}, {messageType}).",
                remoteEndPoint,
                reader,
                messageType);
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }
    }
}
