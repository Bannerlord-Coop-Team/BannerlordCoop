using Coop.Common;
using Coop.Network;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LiteNetLib;

namespace Coop.Multiplayer.Network
{
    public class LiteNetListenerServer : INetEventListener
    {
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
                Log.Info($"Connection request from {request.ToFriendlyString()} rejected: server is full.");
                request.Reject(new byte[] { Convert.ToByte(EDisconnectReason.ServerIsFull) });
                return;
            }
            Log.Info($"Connection request from {request.ToFriendlyString()}.");
            request.Accept();
        }
        public void OnPeerConnected(NetPeer peer)
        {
            var network = new LiteNetConnection(peer);
            var persistence = new RailNetPeerWrapper(network);
            ConnectionServer con = new ConnectionServer(network, persistence, m_WorldData);
            con.PrepareForClientConnection();
            peer.Tag = con;
            m_Server.Connected(con);
        }
        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            m_Server.Disconnected((ConnectionServer)peer.GetConnection(), EDisconnectReason.ClientLeft);
        }
        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            peer.GetConnection().Latency = latency;
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            if (reader.IsNull)
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
            Log.Info($"OnNetworkReceiveUnconnected({remoteEndPoint}, {reader}, {messageType}).");
        }
    }
}
