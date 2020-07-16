using System;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Network.Infrastructure;
using NLog;

namespace Coop.NetImpl.LiteNet
{
    public class LiteNetListenerServer : INetEventListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Server m_Server;
        private readonly ISaveData m_WorldData;

        public LiteNetListenerServer(Server server, ISaveData worldData)
        {
            m_Server = server;
            m_WorldData = worldData;
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            if (!m_Server.CanPlayerJoin())
            {
                Logger.Info(
                    "Connection request from {request} rejected: server is full.",
                    request.ToFriendlyString());
                request.Reject(new[] {Convert.ToByte(EDisconnectReason.ServerIsFull)});
                return;
            }

            Logger.Info("Connection request from {request}.", request.ToFriendlyString());
            request.Accept();
        }

        public void OnPeerConnected(NetPeer peer)
        {
            LiteNetConnection network = new LiteNetConnection(peer);
            RailNetPeerWrapper persistence = new RailNetPeerWrapper(network);
            ConnectionServer con = new ConnectionServer(network, persistence, m_WorldData);
            peer.Tag = con;
            m_Server.Connected(con);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            m_Server.Disconnected(
                (ConnectionServer) peer.GetConnection(),
                disconnectInfo.GetReason(false));
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            peer.GetConnection().Latency = latency;
        }

        public void OnNetworkReceive(
            NetPeer peer,
            NetPacketReader reader,
            DeliveryMethod deliveryMethod)
        {
            if (!reader.IsNull)
            {
                peer.GetConnection().Receive(reader.GetRemainingBytesSegment());
            }
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            Logger.Error("OnNetworkError({endPoint}, {socketError}).", endPoint, socketError);
        }

        public void OnNetworkReceiveUnconnected(
            IPEndPoint remoteEndPoint,
            NetPacketReader reader,
            UnconnectedMessageType messageType)
        {
            Logger.Warn(
                "OnNetworkReceiveUnconnected({remoteEndPoint}, {reader}, {messageType}).",
                remoteEndPoint.ToFriendlyString(),
                reader,
                messageType);
        }
    }
}
