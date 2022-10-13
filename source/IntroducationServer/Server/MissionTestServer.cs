using IntroducationServer.Config;
using IntroducationServer.Data;
using LiteNetLib;
using MissionTestMod;
using NLog;
using System;
using System.Net;
using System.Net.Sockets;

namespace IntroducationServer.Server
{
    public class MissionTestServer : INetEventListener, INatPunchListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public readonly NetManager NetManager;

        private readonly int MaxAllowedPeers = 200;

        private readonly PeerRegistry peerRegistry = new PeerRegistry();

        public MissionTestServer(NetworkConfiguration config)
        {
            NetManager = new NetManager(this)
            {
                NatPunchEnabled = true,
            };
            NetManager.NatPunchModule.Init(this);

            if (config.NATType == NATType.Internal)
            {
                NetManager.Start(config.LanPort);
            }
            else
            {
                NetManager.Start(config.WanPort);
            }
        }

        public void Update()
        {
            NetManager.PollEvents();
            NetManager.NatPunchModule.PollEvents();
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            string token = request.Data.GetString();

            if (ClientInfo.TryParse(token, out ClientInfo clientInfo))
            {
                if (NetManager.ConnectedPeersCount > MaxAllowedPeers)
                {
                    request.Reject();
                    return;
                }

                if (clientInfo.ModVersion != typeof(TestMod).Assembly.GetName().Version)
                {
                    request.Reject();
                    return;
                }

                peerRegistry.RegisterPeer(clientInfo.ClientId, request.Accept());
            }
            else
            {
                request.Reject();
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            Console.WriteLine($"Recieved connection from {peer.EndPoint}");
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Console.WriteLine($"{peer.EndPoint} disconnected. Reason: {disconnectInfo.Reason}");
            peerRegistry.RemovePeer(peer);
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkReceive(
            NetPeer peer,
            NetPacketReader reader,
            DeliveryMethod deliveryMethod)
        {
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
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            ClientInfo clientInfo;
            if (ClientInfo.TryParse(token, out clientInfo) == false) return;

            string instance = clientInfo.InstanceName;
            Guid id = clientInfo.ClientId;

            if (peerRegistry.ContainsP2PPeer(instance, id))
            {
                return;
            }

            foreach (var existingPeer in peerRegistry.GetPeersInInstance(instance))
            {
                Console.WriteLine($"Connecting {localEndPoint} to {existingPeer.InternalAddr}");
                NetManager.NatPunchModule.NatIntroduce(
                    existingPeer.InternalAddr, // host internal
                    existingPeer.ExternalAddr, // host external
                    localEndPoint, // client internal
                    remoteEndPoint, // client external
                    token // request token
                );
            }

            NetPeer peer = peerRegistry.GetPeer(id);

            if (peer != null)
            {
                var p2PPeer = new P2PPeer(peer, localEndPoint, remoteEndPoint);
                peerRegistry.RegisterPeer(instance, p2PPeer);
            }

        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            // Ignore on server
        }
    }
}
