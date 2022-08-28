using System;
using System.Net;
using Common;
using LiteNetLib;
using System.Net.Sockets;
using Common.Components;
using Common.Messaging;
using Coop.Core;
using Coop.Core.Configuration;
using Coop.Core.Client.States;
using Coop.Core.Client.Messages;

namespace Coop.Core.Client
{
    public interface ICoopClient : ICoopNetwork, IUpdateable, INetEventListener
    {
    }

    public class CoopClient : ICoopClient
    {
        private readonly INetworkConfiguration configuration;
        private readonly IClientLogic logic;
        private readonly IMessageBroker messageBroker;

        private readonly NetManager netManager;

        private bool isConnected = false;
        private NetPeer serverPeer;

        public CoopClient(
            INetworkConfiguration config, 
            IClientLogic logic, 
            IMessageBroker messageBroker)
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
                messageBroker.Publish(this, new NetworkDisconnected());
            }
        }

        public void Start()
        {
            if (isConnected)
            {
                Stop();
            }

            netManager.Start();
            serverPeer = netManager.Connect(configuration.Address, configuration.Port, configuration.Token);
        }

        public void Stop()
        {
            logic.Stop();
        }

        public void Update(TimeSpan frameTime)
        {
            netManager.PollEvents();
        }
    }
}