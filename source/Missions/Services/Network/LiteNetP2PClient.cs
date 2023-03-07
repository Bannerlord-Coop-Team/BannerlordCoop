using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using IntroServer.Config;
using IntroServer.Data;
using IntroServer.Server;
using LiteNetLib;
using LiteNetLib.Utils;
using Missions.Services.Agents.Packets;
using Missions.Services.Network.Messages;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Version = System.Version;

namespace Missions.Services.Network
{
    public class LiteNetP2PClient : INatPunchListener, INetEventListener, IUpdateable, IDisposable, INetwork
    {
        private static readonly ILogger _logger = LogManager.GetLogger<LiteNetP2PClient>();
        public int ConnectedPeersCount => _netManager.ConnectedPeersCount;

        public NetPeer PeerServer { get; private set; }
        public int Priority => 2;

        public IPacketManager PacketManager { get; private set; }
        public INetworkConfiguration Configuration { get; }

        private string _instance;

        private readonly Guid id = Guid.NewGuid();
        private readonly BatchLogger<PacketType> _batchLogger = new BatchLogger<PacketType>(LogEventLevel.Verbose, 10000);
        private readonly NetManager _netManager;
        private readonly NetworkConfiguration _networkConfig;
        private readonly Version _version = typeof(MissionTestServer).Assembly.GetName().Version;
        private readonly IMessageBroker _messageBroker;
        private readonly IPacketManager _packetManager;
        private readonly Poller _poller;
        
        public LiteNetP2PClient(NetworkConfiguration config, INetworkMessageBroker messageBroker, IPacketManager packetManager)
        {
            NetworkMessageBroker.Instance.Network = this;

            PacketManager = packetManager;
            _networkConfig = config;
            _messageBroker = messageBroker;

            _netManager = new NetManager(this)
            {
                NatPunchEnabled = true,
                //DisconnectTimeout = config.DisconnectTimeout.Milliseconds,
                //PingInterval = config.PingInterval.Milliseconds,
                //ReconnectDelay = config.ReconnectDelay.Milliseconds,
            };

            _poller = new Poller(Update, TimeSpan.FromMilliseconds(1000 / 120));
            _netManager.NatPunchModule.Init(this);
        }

        ~LiteNetP2PClient()
        {
            Dispose();
        }

        public void Dispose()
        {
            _batchLogger.Dispose();
            Stop();
        }

        public void Start()
        {
            if (_netManager.IsRunning == false)
            {
                _logger.Debug("Starting Client");
                _netManager.Start();
                _poller.Start();
            }
        }

        public void Stop()
        {
            _logger.Debug("Stopping Client");
            _poller.Stop();
            _netManager.DisconnectAll();
            _netManager.Stop();
        }

        public void Update(TimeSpan frameTime)
        {
            _netManager.PollEvents();
            _netManager.NatPunchModule.PollEvents();
        }

        public bool ConnectToP2PServer()
        {
            Start();

            _logger.Information("Connecting to P2P Server");
            string connectionAddress;
            int port;
            if (_networkConfig.NATType == NatAddressType.Internal)
            {
                connectionAddress = _networkConfig.LanAddress.ToString();
                port = _networkConfig.LanPort;
            }
            else
            {
                connectionAddress = _networkConfig.WanAddress.ToString();
                port = _networkConfig.WanPort;
            }

            _logger.Information($"Connecting to {connectionAddress}:{port}");

            ClientInfo clientInfo = new ClientInfo(
                id,
                _version);

            PeerServer = _netManager.Connect(connectionAddress,
                                            port,
                                            clientInfo.ToString());

            Task connectionTask = Task.Run(WaitForConnection);

            return connectionTask.Wait(TimeSpan.FromSeconds(1));
        }

        private async Task WaitForConnection()
        {
            while (PeerServer.ConnectionState != ConnectionState.Connected)
            {
                await Task.Delay(100);
            }
        }

        public void NatPunch(string instance)
        {
            _instance = instance;
            TryPunch(instance);
        }

        private void TryPunch(string instance)
        {
            string token = $"{instance}%{id}";
            if (_networkConfig.NATType == NatAddressType.Internal)
            {
                _netManager.NatPunchModule.SendNatIntroduceRequest(_networkConfig.LanAddress.ToString(), _networkConfig.LanPort, token);
            }
            else if (_networkConfig.NATType == NatAddressType.External)
            {
                _netManager.NatPunchModule.SendNatIntroduceRequest(_networkConfig.WanAddress.ToString(), _networkConfig.WanPort, token);
            }
        }

        public void Send(NetPeer netPeer, IPacket packet)
        {
            NetDataWriter writer = new NetDataWriter();

            try
            {
                writer.PutBytesWithLength(ProtoBufSerializer.Serialize(packet));
                netPeer.Send(writer, packet.DeliveryMethod);
            }
            catch(Exception ex)
            {
                _logger.Error("Serialization failed: {ErrMessage}", ex.Message);
            }
        }

        public void SendAllBut(NetPeer netPeer, IPacket packet)
        {
            foreach (var peer in _netManager.ConnectedPeerList.Where(peer => peer != netPeer))
            {
                Send(peer, packet);
            }
        }

        public void SendAll(IPacket packet)
        {
            NetDataWriter writer = new NetDataWriter();
            writer.PutBytesWithLength(ProtoBufSerializer.Serialize(packet));
            _netManager.SendToAll(writer, packet.DeliveryMethod);
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            // No requests on client
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            if (type == _networkConfig.NATType)
            {
                _logger.Information("Connecting P2P: {TargetEndPoint}", targetEndPoint);
                _netManager.Connect(targetEndPoint, token);
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            if (PeerServer != null && peer.EndPoint != PeerServer.EndPoint)
            {
                Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(.5));
                    var peerConnectedEvent = new PeerConnected(peer);
                    _messageBroker.Publish(this, peerConnectedEvent);
                });
            }
            _logger.Information("{LocalPort} received connection from {peer}", _netManager.LocalPort, peer.EndPoint);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (PeerServer != null && peer != PeerServer)
            {
                var peerDisconnectedEvent = new PeerDisconnected(peer, disconnectInfo);
                _messageBroker.Publish(this, peerDisconnectedEvent);
            }
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {

        }
        
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            IPacket packet = (IPacket)ProtoBufSerializer.Deserialize(reader.GetBytesWithLength());
            _batchLogger.Log(packet.PacketType);
            PacketManager.HandleRecieve(peer, packet);
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {

        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {

        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            string[] data = request.Data.GetString().Split('%');

            if (data.Length != 2) return;

            string instance = data[0];

            if (_instance == instance)
            {
                request.Accept();
            }
            else
            {
                request.Reject();
            }
        }
    }
}
