using System;
using System.Net;
using Common;
using LiteNetLib;
using System.Net.Sockets;
using Common.Components;
using Coop.Communication.PacketHandlers;
using Coop.Configuration;
using Coop.Mod.LogicStates.Client;

namespace Coop.Mod
{
    public interface ICoopClient : ICoopNetwork, IUpdateable, INetEventListener
    {
        bool IsConnected { get; }
    }

    public class CoopClient : ComponentContainerBase, ICoopClient
    {
        private readonly INetworkConfiguration _configuration;
        private readonly IClientLogic _logic;
        private readonly IPacketManager _packetManager;

        private readonly NetManager _netManager;

        public bool IsConnected { get; private set; }

        private NetPeer _serverPeer;

        public CoopClient(INetworkConfiguration config, IClientLogic logic)
        {
            _configuration = config;
            _logic = logic;
            _packetManager = logic.Communicator.PacketManager;

            _netManager = new NetManager(this);
            _packetManager.Initialize(_netManager);
        }

        public int Priority => 0;

        public void Disconnect()
        {
            _serverPeer.Disconnect();
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
            _packetManager.Handle(peer, reader, deliveryMethod);
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
            _netManager.Start();
            _serverPeer = _netManager.Connect(_configuration.Address, _configuration.Port, _configuration.Token);
        }

        public void Stop()
        {
            _netManager.Stop();
        }

        public void Update(TimeSpan frameTime)
        {
            throw new NotImplementedException();
        }
    }
}