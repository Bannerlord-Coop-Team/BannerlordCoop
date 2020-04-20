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
    public class NetListenerServer : INetEventListener
    {
        private readonly Server m_Server;
        private readonly IWorldData m_WorldData;
        public NetListenerServer(Server server, IWorldData worldData)
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
            ConnectionServer con = new ConnectionServer(new NetConnection(peer), m_WorldData);
            con.PrepareForClientConnection();
            peer.Tag = con;
            m_Server.OnConnected(con);
        }
        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            m_Server.OnDisconnected(peer.GetConnection(), EDisconnectReason.ClientLeft);
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
            peer.GetConnection().Receive(new ByteReader(reader.GetRemainingBytes()));
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
