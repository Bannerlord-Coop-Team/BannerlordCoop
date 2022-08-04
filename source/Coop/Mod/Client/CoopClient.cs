using System;
using System.Net;
using Common;
using NLog;
using LiteNetLib;
using System.Net.Sockets;
using Coop.Mod.States.Client;
using Common.MessageBroker;

namespace Coop.Mod
{
    public interface ICoopClient : ICoopNetwork, IUpdateable, INetEventListener 
    {
        bool IsConnected { get; }
    }

    public class CoopClient : ICoopClient
    {
        private readonly INetworkConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IClientContext _context;

        private readonly NetManager _netManager;

        public bool IsConnected { get; private set; }

        private NetPeer _serverPeer;

        public CoopClient(INetworkConfiguration config, IClientContext context)
        {
            _logger = _context.Logger;
            _configuration = config;
            _context = context;

            _netManager = new NetManager(this);
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
            throw new NotImplementedException();
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