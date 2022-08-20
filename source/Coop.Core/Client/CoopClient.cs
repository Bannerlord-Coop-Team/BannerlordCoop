using System;
using System.Net;
using Common;
using LiteNetLib;
using System.Net.Sockets;
using Common.Components;
using Common.Messaging;
using Coop.Core;
using Coop.Core.Messages.Network;
using Coop.Core.Configuration;
using Coop.Core.Client.States;

namespace Coop.Core.Client
{
    public interface ICoopClient : ICoopNetwork, IUpdateable, INetEventListener
    {
        bool IsConnected { get; }
    }

    public class CoopClient : ComponentContainerBase, ICoopClient
    {
        private readonly INetworkConfiguration configuration;
        private readonly IClientLogic logic;
        private readonly IMessageBroker messageBroker;

        private readonly NetManager netManager;

        public bool IsConnected { get; private set; }

        private NetPeer serverPeer;

        public CoopClient(INetworkConfiguration config, IClientLogic logic, IMessageBroker messageBroker)
        {
            configuration = config;
            this.logic = logic;
            this.messageBroker = messageBroker;
            this.netManager = new NetManager(this);
        }

        public int Priority => 0;

        public void Disconnect()
        {
            serverPeer.Disconnect();
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            throw new NotImplementedException();
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
            IsConnected = true;
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            IsConnected = false;
        }

        public void Start()
        {
            if (IsConnected)
            {
                Stop();
            }
            netManager.Start();
            serverPeer = netManager.Connect(configuration.Address, configuration.Port, configuration.Token);
        }

        public void Stop()
        {
            netManager.Stop();
        }

        public void Update(TimeSpan frameTime)
        {
            throw new NotImplementedException();
        }
    }
}