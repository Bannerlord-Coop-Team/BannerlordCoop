using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Coop.Core.Server
{
    public interface ICoopServer : INetwork, INatPunchListener, INetEventListener, IDisposable
    {
        IEnumerable<NetPeer> ConnectedPeers { get; }
        void AllowJoining();
    }

    public class CoopServer : CoopNetworkBase, ICoopServer
    {
        public override int Priority => 0;

        public IEnumerable<NetPeer> ConnectedPeers => netManager.ConnectedPeerList;
        public Guid ServerId { get; } = Guid.NewGuid();

        private readonly IMessageBroker messageBroker;
        private readonly IPacketManager packetManager;
        private readonly IClientRegistry clientRegistry;
        private readonly NetManager netManager;

        private bool allowJoining = false;

        public CoopServer(
            INetworkConfiguration configuration, 
            IMessageBroker messageBroker,
            IPacketManager packetManager,
            IClientRegistry clientOrchestrator) : base(configuration)
        {
            // Dependancy assignment
            this.messageBroker = messageBroker;
            this.packetManager = packetManager;
            this.clientRegistry = clientOrchestrator;

            // TODO add configuration
            netManager = new NetManager(this);

            // Netmanager initialization
            netManager.NatPunchEnabled = true;
            netManager.NatPunchModule.Init(this);
        }

        public void Dispose()
        {
            netManager.DisconnectAll();
            netManager.Stop();
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            if(allowJoining)
            {
                request.Accept();
            }
            else
            {
                request.Reject();
            }
            
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            throw new NotImplementedException();
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            // Not used on server
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
            PlayerConnected message = new PlayerConnected(peer);
            messageBroker.Publish(this, message);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            PlayerDisconnected message = new PlayerDisconnected(peer, disconnectInfo);
            messageBroker.Publish(this, message);
        }

        public override void Update(TimeSpan frameTime)
        {
            netManager.PollEvents();
            netManager.NatPunchModule.PollEvents();
        }

        public override void Start()
        {
            netManager.Start(Configuration.Port);
        }

        public override void Stop()
        {
            netManager.Stop();
        }

        public override void SendAll(IPacket packet)
        {
            SendAll(netManager, packet);
        }

        public override void SendAllBut(NetPeer netPeer, IPacket packet)
        {
            SendAllBut(netManager, netPeer, packet);
        }

        public void AllowJoining()
        {
            allowJoining = true;
        }
    }
}
