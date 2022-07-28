using Common;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.NetImpl.LiteNet
{
    public class LiteNetP2PClient : INatPunchListener, INetEventListener, IUpdateable
    {
        public int ConnectedPeersCount => netManager.ConnectedPeersCount;
        public event EventBasedNetListener.OnNetworkReceive DataRecieved;

        NetManager netManager;
        string instance;
        public NetPeer peerServer { get; private set; }
        public int Priority => 2;

        INetworkConfig networkConfig;
        public LiteNetP2PClient(INetworkConfig networkConfig)
        {
            this.networkConfig = networkConfig;

            netManager = new NetManager(this)
            {
                IPv6Enabled = IPv6Mode.DualMode,
                NatPunchEnabled = true,
            };

            netManager.NatPunchModule.Init(this);

            netManager.Start();
        }

        ~LiteNetP2PClient()
        {
            Stop();
        }

        public void Update(TimeSpan frameTime)
        {
            netManager.PollEvents();
            netManager.NatPunchModule.PollEvents();
        }

        public bool ConnectToP2PServer(string instance)
        {
            this.instance = instance;

            TryPunch(instance);

            peerServer = netManager.Connect(networkConfig.ServerAddress,
                                            networkConfig.P2PPort,
                                            networkConfig.P2PToken);

            return peerServer != null;
        }

        private void TryPunch(string instance)
        {
            netManager.NatPunchModule.SendNatIntroduceRequest(networkConfig.ServerAddress, networkConfig.P2PPort, instance);
        }

        public void Stop()
        {
            netManager.Stop();
        }

        public void SendAll(NetDataWriter writer, DeliveryMethod deliveryMethod)
        {
            netManager.SendToAll(writer, deliveryMethod);
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            // No requests on client
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            if (type == networkConfig.NATType)
            {
                netManager.Connect(targetEndPoint, token);
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            Trace.WriteLine($"{this.netManager.LocalPort} recieved connection from {peer.EndPoint}");
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            DataRecieved?.Invoke(peer, reader, deliveryMethod);
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            request.AcceptIfKey(instance);
        }
    }
}
