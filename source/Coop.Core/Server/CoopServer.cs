using System;
using LiteNetLib;
using System.Net;
using System.Net.Sockets;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Messages.Network;
using Coop.Core.Configuration;

namespace Coop.Core.Server
{
    public interface ICoopServer : ICoopNetwork, INatPunchListener, IDisposable
    {
    }

    public class CoopServer : ICoopServer
    {
        private readonly NetManager netManager;
        private readonly INetworkConfiguration configuration;
        private readonly IMessageBroker messageBroker;

        public CoopServer(INetworkConfiguration configuration, IMessageBroker messageBroker)
        {
            // Dependancy assignment
            this.netManager = new NetManager(this);
            this.configuration = configuration;
            this.messageBroker = messageBroker;

            // Netmanager initialization
            netManager.NatPunchEnabled = true;
            netManager.NatPunchModule.Init(this);
        }

        public int Priority => 0;

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
            ReceivePacket message = new ReceivePacket(peer, reader, deliveryMethod);
            messageBroker.Publish(this, message);
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            throw new NotImplementedException();
        }

        public void OnPeerConnected(NetPeer peer)
        {
            ClientConnectedMessage message = new ClientConnectedMessage(peer);
            messageBroker.Publish(this, message);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            ClientDisconnectedMessage message = new ClientDisconnectedMessage(peer, disconnectInfo);
            messageBroker.Publish(this, message);
        }

        public void Start()
        {
            netManager.Start();
        }

        public void Stop()
        {
            netManager?.Stop();
        }

        public void Update(TimeSpan frameTime)
        {
            netManager.PollEvents();
            netManager.NatPunchModule.PollEvents();
        }
    }
}
