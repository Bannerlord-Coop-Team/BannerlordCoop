using System;
using System.Net;
using Common;
using NLog;
using LiteNetLib;
using System.Net.Sockets;

namespace Coop.Mod
{
    public interface ICoopClient : ICoopNetwork, IUpdateable, INetEventListener { }
    public class CoopClient : ICoopClient
    {
        private readonly INetworkConfiguration _configuration;
        private readonly ILogger _logger;

        public CoopClient(INetworkConfiguration config, ILogger logger)
        {
            _configuration = config;
            _logger = logger;
        }

        public int Priority => 0;

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
            throw new NotImplementedException();
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            throw new NotImplementedException();
        }

        public bool Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void Update(TimeSpan frameTime)
        {
            throw new NotImplementedException();
        }
    }
}