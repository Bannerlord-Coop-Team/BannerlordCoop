using Coop.NetImpl.LiteNet;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Tests.Mission.Dummy
{
    public class LiteNetP2PServer : INatPunchListener, INetEventListener
    {
        public int ConnectedPeersCount => netManager.ConnectedPeersCount;
        INetworkConfig networkConfig;

        NetManager netManager;

        private readonly Dictionary<string, List<P2PPeer>> _instancePeers = new Dictionary<string, List<P2PPeer>>();

        public LiteNetP2PServer(INetworkConfig networkConfig)
        {
            this.networkConfig = networkConfig;

            netManager = new NetManager(this)
            {
                IPv6Enabled = IPv6Mode.DualMode,
                NatPunchEnabled = true,
            };

            netManager.Start(networkConfig.P2PPort);

            netManager.NatPunchModule.Init(this);
        }

        ~LiteNetP2PServer()
        {
            netManager.Stop();
        }

        public void Stop()
        {
            netManager.Stop();
        }

        public void Update()
        {
            netManager.PollEvents();
            netManager.NatPunchModule.PollEvents();
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            var newPeer = new P2PPeer(localEndPoint, remoteEndPoint);
            if (_instancePeers.TryGetValue(token, out var peers))
            {
                if (peers.Contains(newPeer))
                {
                    newPeer.Refresh();
                    return;
                }

                foreach (var existingPeer in peers)
                {
                    Trace.WriteLine($"Connecting {localEndPoint} to {existingPeer.InternalAddr}");
                    netManager.NatPunchModule.NatIntroduce(
                        existingPeer.InternalAddr, // host internal
                        existingPeer.ExternalAddr, // host external
                        localEndPoint, // client internal
                        remoteEndPoint, // client external
                        token // request token
                    );
                }

                _instancePeers[token].Add(newPeer);
            }
            else
            {
                _instancePeers[token] = new List<P2PPeer>
                {
                    newPeer
                };
            }
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            // Ignore on server
        }

        public void OnPeerConnected(NetPeer peer)
        {
            Trace.WriteLine($"Server recieved connection from {peer.EndPoint}");
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Trace.WriteLine($"{peer.EndPoint} has disconnected. Reason: {disconnectInfo.Reason}");
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {

        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {

        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {

        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            request.AcceptIfKey(networkConfig.P2PToken);
        }
    }
}
