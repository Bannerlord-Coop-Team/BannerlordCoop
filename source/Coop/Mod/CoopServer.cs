using System;
using NLog;
using LiteNetLib;
using System.Net;
using System.Net.Sockets;

namespace Coop.Mod
{
    public interface ICoopServer : ICoopNetwork, INatPunchListener, IDisposable { }

    public class CoopServer : ICoopServer
    {
        private readonly ILogger _logger;
        private readonly INetworkConfiguration _configuration;

        private NetManager m_NetManager;

        public CoopServer(ILogger logger, INetworkConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            m_NetManager = new NetManager(this);
            m_NetManager.NatPunchEnabled = true;
        }

        public int Priority => throw new NotImplementedException();

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            throw new NotImplementedException();
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            throw new NotImplementedException();
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
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
            m_NetManager.PollEvents();
            m_NetManager.NatPunchModule.PollEvents();
        }
    }
}
