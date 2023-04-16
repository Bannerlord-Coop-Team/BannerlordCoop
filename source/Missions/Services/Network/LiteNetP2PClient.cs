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
using Missions.Services.Network.Messages;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Version = System.Version;

namespace Missions.Services.Network
{
    public class LiteNetP2PClient : INatPunchListener, INetEventListener, IUpdateable, IDisposable, INetwork
    {
        private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();
        public int ConnectedPeersCount => netManager.ConnectedPeersCount;

        public NetPeer PeerServer { get; private set; }
        public int Priority => 2;

        public IPacketManager PacketManager { get; private set; }
        public INetworkConfiguration Configuration { get; }

        private string instance;

        private readonly Guid id = Guid.NewGuid();
        private readonly BatchLogger<PacketType> _batchLogger = new BatchLogger<PacketType>(LogEventLevel.Verbose, 10000);
        private readonly NetManager netManager;
        private readonly NetworkConfiguration networkConfig;
        private readonly Version version = typeof(MissionTestServer).Assembly.GetName().Version;
        private readonly IMessageBroker messageBroker;
        private readonly Poller poller;
        
        public LiteNetP2PClient(NetworkConfiguration config, INetworkMessageBroker messageBroker, IPacketManager packetManager)
        {
            NetworkMessageBroker.Instance.Network = this;

            PacketManager = packetManager;
            networkConfig = config;
            this.messageBroker = messageBroker;

            netManager = new NetManager(this)
            {
                NatPunchEnabled = true,
                //DisconnectTimeout = config.DisconnectTimeout.Milliseconds,
                //PingInterval = config.PingInterval.Milliseconds,
                //ReconnectDelay = config.ReconnectDelay.Milliseconds,
            };

            poller = new Poller(Update, TimeSpan.FromMilliseconds(1000 / 120));
            netManager.NatPunchModule.Init(this);
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
            if (netManager.IsRunning == false)
            {
                Logger.Debug("Starting Client");
                netManager.Start();
                poller.Start();
            }
        }

        public void Stop()
        {
            Logger.Debug("Stopping Client");
            poller.Stop();
            netManager.DisconnectAll();
            netManager.Stop();
        }

        public void Update(TimeSpan frameTime)
        {
            netManager.PollEvents();
            netManager.NatPunchModule.PollEvents();
        }

        public bool ConnectToP2PServer()
        {
            Start();

            Logger.Information("Connecting to P2P Server");
            string connectionAddress;
            int port;
            if (networkConfig.NATType == NatAddressType.Internal)
            {
                connectionAddress = networkConfig.LanAddress.ToString();
                port = networkConfig.LanPort;
            }
            else
            {
                connectionAddress = networkConfig.WanAddress.ToString();
                port = networkConfig.WanPort;
            }

            Logger.Information($"Connecting to {connectionAddress}:{port}");

            ClientInfo clientInfo = new ClientInfo(
                id,
                version);

            PeerServer = netManager.Connect(connectionAddress,
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
            this.instance = instance;
            TryPunch(instance);
        }

        private void TryPunch(string instance)
        {
            string token = $"{instance}%{id}";
            if (networkConfig.NATType == NatAddressType.Internal)
            {
                netManager.NatPunchModule.SendNatIntroduceRequest(networkConfig.LanAddress.ToString(), networkConfig.LanPort, token);
            }
            else if (networkConfig.NATType == NatAddressType.External)
            {
                netManager.NatPunchModule.SendNatIntroduceRequest(networkConfig.WanAddress.ToString(), networkConfig.WanPort, token);
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
                Logger.Error("Serialization failed: {ErrMessage}", ex.Message);
            }
        }

        public void SendAllBut(NetPeer netPeer, IPacket packet)
        {
            foreach (var peer in netManager.ConnectedPeerList.Where(peer => peer != netPeer))
            {
                Send(peer, packet);
            }
        }

        public void SendAll(IPacket packet)
        {
            NetDataWriter writer = new NetDataWriter();
            writer.PutBytesWithLength(ProtoBufSerializer.Serialize(packet));
            netManager.SendToAll(writer, packet.DeliveryMethod);
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            // No requests on client
        }

        private static Dictionary<string, NatAddressType> TokenToNatTypeMap = new Dictionary<string, NatAddressType>
        {
            { "Internal", NatAddressType.Internal },
            { "External", NatAddressType.External }
        };
        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            string[] tokens = token.Split('%');

            if(tokens.Length != 3)
            {
                // Invalid token length
                return;
            }

            if (TokenToNatTypeMap.TryGetValue(tokens[0], out NatAddressType expectedNatType) == false)
            {
                // String does not exist in map
                return;
            }

            if (type == expectedNatType)
            {
                Logger.Information("Connecting P2P: {TargetEndPoint}", targetEndPoint);
                netManager.Connect(targetEndPoint, token);
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
                    messageBroker.Publish(this, peerConnectedEvent);
                });
            }
            Logger.Information("{LocalPort} received connection from {peer}", netManager.LocalPort, peer.EndPoint);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (PeerServer != null && peer != PeerServer)
            {
                var peerDisconnectedEvent = new PeerDisconnected(peer, disconnectInfo);
                messageBroker.Publish(this, peerDisconnectedEvent);
            }
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {

        }
        
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            IPacket packet = (IPacket)ProtoBufSerializer.Deserialize(reader.GetBytesWithLength());
            _batchLogger.Log(packet.PacketType);

            if(packet.PacketType == PacketType.Event)
            {

            }

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

            if (data.Length != 3) return;

            string instance = data[1];

            if (this.instance == instance)
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
