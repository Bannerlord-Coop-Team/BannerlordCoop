using System;
using NLog;
using LiteNetLib;
using System.Net;
using System.Net.Sockets;
using Coop.Mod.States.Server;
using Common.MessageBroker;

namespace Coop.Mod
{
    public interface ICoopServer : ICoopNetwork, INatPunchListener, IDisposable { }

    public class CoopServer : ICoopServer
    {
        private readonly ILogger _logger;
        private readonly INetworkConfiguration _configuration;
        private readonly IServerContext _context;
        private readonly IPacketManager _packetManager;

        private NetManager m_NetManager;

        public CoopServer(INetworkConfiguration configuration, IServerContext context)
        {
            // Dependancy assignment
            _logger = context.Logger;
            _configuration = configuration;
            _packetManager = context.PacketManager;
            _context = context;

            // Netmanager initialization
            m_NetManager = new NetManager(this)
            {
                NatPunchEnabled = true
            };
        }

        public int Priority => 0;

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
            _packetManager.Handle(peer, reader, deliveryMethod);
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

        public void Start()
        {
            m_NetManager.Start();
        }

        public void Stop()
        {
            m_NetManager?.Stop();
        }

        public void Update(TimeSpan frameTime)
        {
            m_NetManager.PollEvents();
            m_NetManager.NatPunchModule.PollEvents();
        }
    }
}
