using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Missions.Network;
using NLog;
using LiteNetLib;
using Missions.Config;

namespace MissionTestMod.Server
{
    public class MissionTestServer : INetEventListener, INatPunchListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, List<P2PPeer>> m_instancePeers = new Dictionary<string, List<P2PPeer>>();

        public readonly NetManager NetManager;

        private readonly NetworkConfiguration config;

        public MissionTestServer(NetworkConfiguration config)
        {
            this.config = config;
            NetManager = new NetManager(this);
            NetManager.NatPunchModule.Init(this);

            NetManager.Start(config.WanPort);
        }

        public void Update()
        {
            NetManager.PollEvents();
            NetManager.NatPunchModule.PollEvents();
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            request.Accept();
        }

        public void OnPeerConnected(NetPeer peer)
        {
            Console.WriteLine($"Recieved connection from {peer.EndPoint}");
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Console.WriteLine($"{peer.EndPoint} disconnected. Reason: {disconnectInfo.Reason}");
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
