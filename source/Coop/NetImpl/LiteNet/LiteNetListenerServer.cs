using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Network.Infrastructure;
using NLog;

namespace Coop.NetImpl.LiteNet
{
    public class LiteNetListenerServer : INetEventListener, INatPunchListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Server m_Server;

        private readonly Dictionary<string, List<P2PPeer>> m_instancePeers = new Dictionary<string, List<P2PPeer>>();

        public readonly NetManager NetManager;

        public LiteNetListenerServer(Server server, NetworkConfiguration config)
        {
            m_Server = server;
            NetManager = NetManagerFactory.Create(this, config);
            NetManager.NatPunchModule.Init(this);
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
            ConnectionServer con = new ConnectionServer(network, persistence);
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

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            var newPeer = new P2PPeer(localEndPoint, remoteEndPoint);
            if (m_instancePeers.TryGetValue(token, out var peers))
            {
                if (peers.Contains(newPeer))
                {
                    newPeer.Refresh();
                    return;
                }

                foreach (var existingPeer in peers)
                {
                    Trace.WriteLine($"Connecting {localEndPoint} to {existingPeer.InternalAddr}");
                    NetManager.NatPunchModule.NatIntroduce(
                        existingPeer.InternalAddr, // host internal
                        existingPeer.ExternalAddr, // host external
                        localEndPoint, // client internal
                        remoteEndPoint, // client external
                        token // request token
                    );
                }

                m_instancePeers[token].Add(newPeer);
            }
            else
            {
                m_instancePeers[token] = new List<P2PPeer>
                {
                    newPeer
                };
            }
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            // Ignore on server
        }
    }
}
