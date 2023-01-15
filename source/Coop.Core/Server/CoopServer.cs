using System;
using LiteNetLib;
using System.Net;
using System.Net.Sockets;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Configuration;
using Coop.Core.Communication.PacketHandlers;
using Common.Serialization;
using LiteNetLib.Utils;
using System.Linq;
using Coop.Core.Communication.Network;
using Coop.Core.Server.States;
using Coop.Core.Server.Connections.Messages;

namespace Coop.Core.Server
{
    public interface ICoopServer : ICoopNetwork, INatPunchListener, INetEventListener, IDisposable
    {
    }

    public class CoopServer : CoopNetworkBase, ICoopServer
    {
        public override int Priority => 0;

        public override INetworkConfiguration Configuration { get; }

        private readonly IMessageBroker messageBroker;
        private readonly IPacketManager packetManager;
        private readonly IClientStateOrchestrator clientOrchestrator;
        private readonly IServerLogic logic;
        private readonly NetManager netManager;

        public CoopServer(
            INetworkConfiguration configuration, 
            IMessageBroker messageBroker,
            IPacketManager packetManager,
            IClientStateOrchestrator clientOrchestrator,
            IServerLogic logic)
        {
            // Dependancy assignment
            Configuration = configuration;
            this.messageBroker = messageBroker;
            this.packetManager = packetManager;
            this.clientOrchestrator = clientOrchestrator;
            this.logic = logic;

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
            request.Accept();
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
            throw new NotImplementedException();
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
            netManager.Start();
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
    }
}
