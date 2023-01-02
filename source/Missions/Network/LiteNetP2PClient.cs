using Common;
using Common.Serialization;
using IntroServer.Config;
using IntroServer.Data;
using IntroServer.Server;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Common.Logging;
using Serilog;
using Serilog.Events;
using Version = System.Version;

namespace Missions.Network
{
    public class LiteNetP2PClient : INatPunchListener, INetEventListener, IUpdateable, IDisposable
    {
        private static readonly ILogger m_Logger = LogManager.GetLogger<LiteNetP2PClient>();
        private static readonly Dictionary<PacketType, List<IPacketHandler>> PacketHandlers = new Dictionary<PacketType, List<IPacketHandler>>();
        
        public int ConnectedPeersCount => _netManager.ConnectedPeersCount;
        public event Action<NetPeer, DisconnectInfo> OnClientDisconnected;
        public event Action<NetPeer> OnClientConnected;

        public NetPeer PeerServer { get; private set; }
        public int Priority => 2;

        private string _instance;

        private readonly Guid id = Guid.NewGuid();
        private readonly BatchLogger<PacketType> _batchLogger = new BatchLogger<PacketType>(LogEventLevel.Verbose);
        private readonly NetManager _netManager;
        private readonly NetworkConfiguration _networkConfig;
        private readonly Version _version = typeof(MissionTestServer).Assembly.GetName().Version;
		
        public LiteNetP2PClient(NetworkConfiguration config)
        {
            _networkConfig = config;

            _netManager = new NetManager(this)
            {
                NatPunchEnabled = true,
                DisconnectTimeout = config.DisconnectTimeout.Milliseconds,
                PingInterval = config.PingInterval.Milliseconds,
                ReconnectDelay = config.ReconnectDelay.Milliseconds,
            };

            _netManager.NatPunchModule.Init(this);

            _netManager.Start();
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

        public void AddHandler(IPacketHandler handler)
        {
            if (PacketHandlers.ContainsKey(handler.PacketType))
            {
                PacketHandlers[handler.PacketType].Add(handler);
            }
            else
            {
                PacketHandlers.Add(handler.PacketType, new List<IPacketHandler> { handler });
            }
        }

        public void RemoveHandler(IPacketHandler handler)
        {
            if (PacketHandlers.TryGetValue(handler.PacketType, out List<IPacketHandler> list))
            {
                list.Remove(handler);
            }
        }

        public void Update(TimeSpan frameTime)
        {
            _netManager.PollEvents();
            _netManager.NatPunchModule.PollEvents();
        }

        public bool ConnectToP2PServer()
        {
            string connectionAddress;
            int port;
            if (_networkConfig.NATType == NATType.Internal)
            {
                connectionAddress = _networkConfig.LanAddress.ToString();
                port = _networkConfig.LanPort;
            }
            else
            {
                connectionAddress = _networkConfig.WanAddress.ToString();
                port = _networkConfig.WanPort;
            }

            ClientInfo clientInfo = new ClientInfo(
                id,
                _version);

            PeerServer = _netManager.Connect(connectionAddress,
                                            port,
                                            clientInfo.ToString());

            return PeerServer != null;
        }

        public void NatPunch(string instance)
        {
            _instance = instance;
            TryPunch(instance);
        }

        private void TryPunch(string instance)
        {
            string token = $"{instance}%{id}";
            if (_networkConfig.NATType == NATType.Internal)
            {
                _netManager.NatPunchModule.SendNatIntroduceRequest(_networkConfig.LanAddress.ToString(), _networkConfig.LanPort, token);
            }
            else if (_networkConfig.NATType == NATType.External)
            {
                _netManager.NatPunchModule.SendNatIntroduceRequest(_networkConfig.WanAddress.ToString(), _networkConfig.WanPort, token);
            }
        }

        public void Stop()
        {
            _netManager.DisconnectAll();
            _netManager.Stop();
        }

        public void Send(IPacket packet, NetPeer client)
        {
            //if (netManager.ConnectedPeersCount < 1) return;
            NetDataWriter writer = new NetDataWriter();
            writer.PutBytesWithLength(ProtoBufSerializer.Serialize(packet));
            client.Send(writer, packet.DeliveryMethod);
        }

        public void SendAll(IPacket packet)
        {
            //if (netManager.ConnectedPeersCount < 1) return;
            NetDataWriter writer = new NetDataWriter();
            writer.PutBytesWithLength(ProtoBufSerializer.Serialize(packet));
            _netManager.SendToAll(writer, packet.DeliveryMethod);
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            // No requests on client
        }

        static readonly Dictionary<NATType, NatAddressType> natAddressTypeMap = new Dictionary<NATType, NatAddressType>()
        {
            { NATType.External, NatAddressType.External },
            { NATType.Internal, NatAddressType.Internal },
        };

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            if (type == natAddressTypeMap[_networkConfig.NATType])
            {
                m_Logger.Information("Connecting P2P: {TargetEndPoint}", targetEndPoint);
                _netManager.Connect(targetEndPoint, token);
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            if (PeerServer != null && peer != PeerServer)
            {
                OnClientConnected?.Invoke(peer);
            }
            m_Logger.Information("{LocalPort} received connection from {peer}", _netManager.LocalPort, peer.EndPoint);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            foreach (var handlers in PacketHandlers.Values)
            {
                foreach (var handler in handlers)
                {
                    handler.HandlePeerDisconnect(peer, disconnectInfo);
                }
            }
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {

        }
        
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            IPacket packet = (IPacket)ProtoBufSerializer.Deserialize(reader.GetBytesWithLength());
            if (packet.Data == null) throw new NullReferenceException($"{packet.GetType()} is missing data, likely missing a ProtoMember attribute.");
            if (PacketHandlers.TryGetValue(packet.PacketType, out var handlers))
            {
                _batchLogger.Log(packet.PacketType);
                foreach (var handler in handlers)
                {
                    handler.HandlePacket(peer, packet);
                }
            }
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
