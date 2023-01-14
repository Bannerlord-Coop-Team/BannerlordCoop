using System;
using LiteNetLib;
using System.Net;
using System.Net.Sockets;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Configuration;
using Coop.Core.Communication.PacketHandlers;

namespace Coop.Core.Server
{
    public interface ICoopServer : ICoopNetwork, INatPunchListener, IDisposable
    {
    }

    public class CoopServer : ICoopServer
    {
        public int Priority => 0;

        public IPacketManager PacketManager { get; }

        private readonly INetworkConfiguration configuration;
        private readonly IMessageBroker messageBroker;
        private readonly NetManager netManager;

        public CoopServer(
            INetworkConfiguration configuration, 
            IMessageBroker messageBroker)
        {
            // Dependancy assignment
            this.configuration = configuration;
            this.messageBroker = messageBroker;

            // TODO add configuration
            netManager = new NetManager(this);
            PacketManager = new PacketManager(netManager);

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
            PacketManager.HandleRecieve(peer, reader);
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            throw new NotImplementedException();
        }

        public void OnPeerConnected(NetPeer peer)
        {
            ClientConnected message = new ClientConnected(peer);
            messageBroker.Publish(this, message);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            ClientDisconnected message = new ClientDisconnected(peer, disconnectInfo);
            messageBroker.Publish(this, message);
        }

        public void Start()
        {
            netManager.Start();
        }

        public void Stop()
        {
            netManager.Stop();
        }

        public void Update(TimeSpan frameTime)
        {
            netManager.PollEvents();
            netManager.NatPunchModule.PollEvents();
        }
    }
}
