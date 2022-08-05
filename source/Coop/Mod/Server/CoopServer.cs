using System;
using NLog;
using LiteNetLib;
using System.Net;
using System.Net.Sockets;
using Coop.Mod.LogicStates.Server;
using Common.MessageBroker;
using Common.Components;

namespace Coop.Mod
{
    public interface ICoopServer : ICoopNetwork, INatPunchListener, IDisposable
    {
    }

    public class CoopServer : ComponentContainerBase, ICoopServer
    {
        private readonly ILogger _logger;
        private readonly INetworkConfiguration _configuration;
        private readonly IServerLogic _logic;
        private readonly ICommunicator _communicator;

        private NetManager m_NetManager;

        public CoopServer(INetworkConfiguration configuration, IServerLogic logic)
        {
            // Dependancy assignment
            _logger = logic.Logger;
            _configuration = configuration;
            _logic = logic;
            _communicator = logic.Communicator;

            // Netmanager initialization
            m_NetManager = new NetManager(this)
            {
                NatPunchEnabled = true
            };

            _communicator.PacketManager.Init(m_NetManager);
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
            _communicator.PacketManager.Handle(peer, reader, deliveryMethod);
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
