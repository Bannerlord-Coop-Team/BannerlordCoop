using IntroServer.Config;
using IntroServer.Data;
using LiteNetLib;
using NLog;
using System;
using System.Net;
using System.Net.Sockets;

namespace IntroServer.Server
{
    public class MissionTestServer : INetEventListener, INatPunchListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly NetManager _netManager;

        private const int MaxAllowedPeers = 200;

        private readonly PeerRegistry _peerRegistry = new PeerRegistry();

        public MissionTestServer(NetworkConfiguration config)
        {
            _netManager = new NetManager(this)
            {
                NatPunchEnabled = true,
                DisconnectTimeout = config.DisconnectTimeout.Milliseconds,
                PingInterval = config.PingInterval.Milliseconds,
                ReconnectDelay = config.ReconnectDelay.Milliseconds,
            };
            _netManager.NatPunchModule.Init(this);

            _netManager.Start(config.NATType == NATType.Internal 
	            ? config.LanPort : config.WanPort);
        }

        public void Update()
        {
            _netManager.PollEvents();
            _netManager.NatPunchModule.PollEvents();
        }

        private static readonly Version LocalVersion = typeof(MissionTestServer).Assembly.GetName().Version;

		public void OnConnectionRequest(ConnectionRequest request)
        {
            Logger.Trace("Connection request received for {Peer}", request.RemoteEndPoint);
            string token = request.Data.GetString();

            if (ClientInfo.TryParse(token, out ClientInfo clientInfo))
            {
                if (_netManager.ConnectedPeersCount > MaxAllowedPeers)
                {
                    Logger.Warn("Connection Request Rejected from {Peer} because {RejectionReason}", 
	                    request.RemoteEndPoint, "Max Peers Reached");
                    request.Reject();
                    return;
                }

                if (clientInfo.ModVersion != LocalVersion)
                {

	                Logger.Warn("Connection Request Rejected from {Peer} because {RejectionReason}", 
		                request.RemoteEndPoint, $"Incompatible Mod Version (Local Version: {LocalVersion}, Peer Version: {clientInfo.ModVersion})");
					request.Reject();
                    return;
                }

                _peerRegistry.RegisterPeer(clientInfo.ClientId, request.Accept());
                Logger.Info("Connection Request Accepted for {ClientID} on {Peer}", clientInfo.ClientId, request.RemoteEndPoint);
            }
            else
            {
                Logger.Warn("Connection Request Rejected from {Peer} because {RejectionReason}",
	                request.RemoteEndPoint, "Invalid ClientInfo Provided");
                request.Reject();
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            Logger.Info("Received connection from {Peer}", peer.EndPoint);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Logger.Info("{Peer} disconnected. Reason: {DisconnectionReason}", peer.EndPoint, disconnectInfo.Reason);
            _peerRegistry.RemovePeer(peer);
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            if (latency > 0)
				Logger.Debug("Network latency update of {Latency} for {Peer}", latency, peer.EndPoint);
        }

        public void OnNetworkReceive(
            NetPeer peer,
            NetPacketReader reader,
            DeliveryMethod deliveryMethod)
        {
            
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            Logger.Error("OnNetworkError({endPoint}, {socketError}).", endPoint, socketError);
        }

        public void OnNetworkReceiveUnconnected(
            IPEndPoint remoteEndPoint,
            NetPacketReader reader,
            UnconnectedMessageType messageType)
        {
            Logger.Debug("{Peer} unconnected.", remoteEndPoint);
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            try
            {
                string[] data = token.Split('%');

                if (data.Length != 2) return;

                string instance = data[0];
                if (Guid.TryParse(data[1], out Guid id) == false) return;

                if (_peerRegistry.ContainsP2PPeer(instance, id))
                {
                    return;
                }

                foreach (var existingPeer in _peerRegistry.GetPeersInInstance(instance))
                {
                    Logger.Info("Connecting {LocalAgent} to {Peer}", localEndPoint, existingPeer.InternalAddr);
                    _netManager.NatPunchModule.NatIntroduce(
                        existingPeer.InternalAddr, // host internal
                        existingPeer.ExternalAddr, // host external
                        localEndPoint, // client internal
                        remoteEndPoint, // client external
                        token // request token
                    );
                }

                NetPeer peer = _peerRegistry.GetPeer(id);

                if (peer == null) return;
                var p2PPeer = new P2PPeer(peer, localEndPoint, remoteEndPoint);
                _peerRegistry.RegisterPeer(instance, p2PPeer);
                Logger.Debug("Peer {Peer} Registered", p2PPeer.NetPeer.EndPoint);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error while handling NAT introduction: {ErrorMessage}", ex.Message);
            }

        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            Logger.Trace("Nat Introduction succeeded for {Peer}", targetEndPoint);
        }
    }
}
