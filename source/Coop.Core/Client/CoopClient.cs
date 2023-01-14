using System;
using System.Net;
using Common;
using LiteNetLib;
using System.Net.Sockets;
using Common.Messaging;
using Coop.Core.Configuration;
using Coop.Core.Client.States;
using Coop.Core.Client.Messages;
using Coop.Core.Communication.PacketHandlers;
using Serilog;
using Common.Logging;

namespace Coop.Core.Client
{
    public interface ICoopClient : ICoopNetwork, IUpdateable, INetEventListener
    {
        void SendEvent(INetworkEvent networkEvent);
    }

    public class CoopClient : ICoopClient
    {
        public int Priority => 0;
        
        
        private static readonly ILogger Logger = LogManager.GetLogger<CoopClient>();

        private readonly INetworkConfiguration configuration;
        private readonly IClientLogic logic;
        private readonly IMessageBroker messageBroker;
        private readonly NetManager netManager;
        public IPacketManager PacketManager { get; }

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
            
            // TODO add configuration
            netManager = new NetManager(this);
            PacketManager = new PacketManager(netManager);
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

        /// <summary>
        /// Sends an event to the server over the network
        /// </summary>
        /// <param name="networkEvent">Client event</param>
        public void SendEvent(INetworkEvent networkEvent)
        {
            if(serverPeer == null)
            {
                Logger.Error("Tried to send event while disconnected from server");
                return;
            }

            EventPacket eventPacket = new EventPacket(networkEvent);

            PacketManager.Send(serverPeer, eventPacket);
        }
    }
}