using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Network;
using LiteNetLib;
using Serilog;
using System;
using System.Net;
using System.Net.Sockets;

namespace Coop.Core.Client
{
    public interface ICoopClient : INetwork, IUpdateable, INetEventListener
    {
    }

    public class CoopClient : CoopNetworkBase, ICoopClient
    {
        public override int Priority => 0;
        
        
        private static readonly ILogger Logger = LogManager.GetLogger<CoopClient>();

        private readonly IMessageBroker messageBroker;
        private readonly IPacketManager packetManager;
        private readonly NetManager netManager;

        private bool isConnected = false;
        private NetPeer serverPeer;

        public CoopClient(
            INetworkConfiguration config,
            IMessageBroker messageBroker,
            IPacketManager packetManager) : base(config)
        {
            this.messageBroker = messageBroker;
            this.packetManager = packetManager;

            // TODO add configuration
            netManager = new NetManager(this);
        }

        public void Disconnect()
        {
            serverPeer.Disconnect();
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            request.Reject();
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            throw new NotImplementedException();
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            IPacket packet = (IPacket)ProtoBufSerializer.Deserialize(reader.GetBytesWithLength());
            packetManager.HandleRecieve(peer, packet);
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            throw new NotImplementedException();
        }

        public void OnPeerConnected(NetPeer peer)
        {
            if(isConnected == false)
            {
                isConnected = true;

                messageBroker.Publish(this, new NetworkConnected());
            }
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (isConnected == true)
            {
                isConnected = false;
                messageBroker.Publish(this, new NetworkDisconnected(disconnectInfo));
            }
        }

        public override void Start()
        {
            if (isConnected)
            {
                Stop();
            }

            netManager.Start();
            serverPeer = netManager.Connect(Configuration.Address, Configuration.Port, Configuration.Token);
        }

        public override void Stop()
        {
            netManager.Stop();
        }

        public override void Update(TimeSpan frameTime)
        {
            netManager.PollEvents();
        }

        public override void SendAll(IPacket packet)
        {
            SendAll(netManager, packet);
        }

        public override void SendAllBut(NetPeer netPeer, IPacket packet)
        {
            SendAllBut(netManager, netPeer, packet);
        }
    }
}