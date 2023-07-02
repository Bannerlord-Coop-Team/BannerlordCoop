﻿using IntroServer.Config;
using IntroServer.Data;
using LiteNetLib;
using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Common.Network.Data;

namespace IntroServer.Server
{
    public class MissionTestServer : INetEventListener, INatPunchListener
    {
	    private const int MaxAllowedPeers = 200;

	    private readonly ILogger _logger;
		private readonly NetManager _netManager;
	    private readonly PeerRegistry _peerRegistry = new PeerRegistry();

        private readonly Version _version = typeof(MissionTestServer).Assembly.GetName().Version;

        public MissionTestServer(NetworkConfiguration config, ILogger<MissionTestServer> logger)
        {
	        _logger = logger;
	        _netManager = new NetManager(this)
            {
                NatPunchEnabled = true,
                //DisconnectTimeout = config.DisconnectTimeout.Milliseconds,
                //PingInterval = config.PingInterval.Milliseconds,
                //ReconnectDelay = config.ReconnectDelay.Milliseconds,
            };
            _netManager.NatPunchModule.Init(this);

            if(_netManager.Start(config.NATType == NatAddressType.Internal 
	            ? config.LanPort : config.WanPort) == false)
            {
                _logger.LogError("Failed to start server");
            }
        }

        public void Update()
        {
            _netManager.PollEvents();
            _netManager.NatPunchModule.PollEvents();
        }

        private static readonly Version LocalVersion = typeof(MissionTestServer).Assembly.GetName().Version;

		public void OnConnectionRequest(ConnectionRequest request)
        {
            _logger.LogTrace("Connection request received for {Peer}", request.RemoteEndPoint);
            string token = request.Data.GetString();

            if (ClientInfo.TryParse(token, out ClientInfo clientInfo))
            {
                if (_netManager.ConnectedPeersCount > MaxAllowedPeers)
                {
                    _logger.LogWarning("Connection Request Rejected from {Peer} because {RejectionReason}", 
	                    request.RemoteEndPoint, "Max Peers Reached");
                    request.Reject();
                    return;
                }

                if (clientInfo.ModVersion != _version)
                {

	                _logger.LogWarning("Connection Request Rejected from {Peer} because {RejectionReason}", 
		                request.RemoteEndPoint, $"Incompatible Mod Version (Local Version: {LocalVersion}, Peer Version: {clientInfo.ModVersion})");
					request.Reject();
                    return;
                }

                _peerRegistry.RegisterPeer(clientInfo.ClientId, request.Accept());
                _logger.LogInformation("Connection Request Accepted for {ClientID} on {Peer}", clientInfo.ClientId, request.RemoteEndPoint);
            }
            else
            {
                _logger.LogWarning("Connection Request Rejected from {Peer} because {RejectionReason}",
	                request.RemoteEndPoint, "Invalid ClientInfo Provided");
                request.Reject();
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            _logger.LogInformation("Received connection from {Peer}", peer.EndPoint);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _logger.LogInformation("{Peer} disconnected. Reason: {DisconnectionReason}", peer.EndPoint, disconnectInfo.Reason);
            _peerRegistry.RemovePeer(peer);
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            _logger.LogError("OnNetworkError({endPoint}, {socketError}).", endPoint, socketError);
        }

        public void OnNetworkReceiveUnconnected(
            IPEndPoint remoteEndPoint,
            NetPacketReader reader,
            UnconnectedMessageType messageType)
        {
            _logger.LogDebug("{Peer} unconnected.", remoteEndPoint);
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            try
            {
                if(ConnectionToken.TryParse(token, out var connectionToken) == false)
                {
                    _logger.LogWarning("Unable to parse token: {tokenString}", token);
                    return;
                }

                var instance = connectionToken.InstanceName;
                var id = connectionToken.PeerId;

                if (_peerRegistry.ContainsP2PPeer(instance, id))
                {
                    return;
                }

                foreach (var existingPeer in _peerRegistry.GetPeersInInstance(instance))
                {
                    

                    if(existingPeer.ExternalAddr.Address == remoteEndPoint.Address)
                    {
                        _logger.LogInformation("Connecting internally {LocalAgent} to {Peer}", localEndPoint, existingPeer.InternalAddr);
                    }
                    else
                    {
                        _logger.LogInformation("Connecting externally {LocalAgent} to {Peer}", remoteEndPoint, existingPeer.ExternalAddr);
                    }

                    var p2pNatType = existingPeer.ExternalAddr.Address == remoteEndPoint.Address ? NatAddressType.Internal : NatAddressType.External;
                    var newToken = new ConnectionToken(id, instance, p2pNatType);

                    
                    _netManager.NatPunchModule.NatIntroduce(
                        existingPeer.InternalAddr, // host internal
                        existingPeer.ExternalAddr, // host external
                        localEndPoint, // client internal
                        remoteEndPoint, // client external
                        newToken // request token
                    );
                }

                NetPeer peer = _peerRegistry.GetPeer(id);

                if (peer == null) return;
                var p2PPeer = new P2PPeer(peer, localEndPoint, remoteEndPoint);
                _peerRegistry.RegisterPeer(instance, p2PPeer);
                _logger.LogDebug("Peer {Peer} Registered", p2PPeer.NetPeer.EndPoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while handling NAT introduction: {ErrorMessage}", ex.Message);
            }

        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            _logger.LogTrace("Nat Introduction succeeded for {Peer}", targetEndPoint);
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            // TODO create packet forwarding for strict nat
        }
    }
}
