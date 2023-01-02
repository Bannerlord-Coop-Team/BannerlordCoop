using Common;
using Common.Messaging;
using Common.Serialization;
using IntroServer.Config;
using IntroServer.Data;
using IntroServer.Server;
using LiteNetLib;
using LiteNetLib.Utils;
using Missions.Messages.Network;
using Missions.Packets.Events;
using NLog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Common.Logging;
using Version = System.Version;

namespace Missions.Network
{
    public class LiteNetP2PClient : INatPunchListener, INetEventListener, IUpdateable, IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Dictionary<PacketType, List<IPacketHandler>> PacketHandlers = new Dictionary<PacketType, List<IPacketHandler>>();

        public int ConnectedPeersCount => _netManager.ConnectedPeersCount;
        public event Action<NetPeer, DisconnectInfo> OnClientDisconnected;
        public event Action<NetPeer> OnClientConnected;

        public NetPeer PeerServer { get; private set; }
        public int Priority => 2;

        private string _instance;

        private readonly Guid id = Guid.NewGuid();
        private readonly BatchLogger<PacketType> _batchLogger = new BatchLogger<PacketType>(LogLevel.Trace);
        private readonly NetManager _netManager;
        private readonly NetworkConfiguration _networkConfig;
        private readonly Version _version = typeof(MissionTestServer).Assembly.GetName().Version;
        private readonly IMessageBroker _messageBroker;
        public LiteNetP2PClient(NetworkConfiguration configuration, IMessageBroker messageBroker)
        {
            _networkConfig = configuration;
            _messageBroker = messageBroker;

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

        public void SendEvent(INetworkEvent networkEvent, NetPeer peer)
        {
            EventPacket eventPacket = new EventPacket(networkEvent);

            Send(eventPacket, peer);
        }

        public void SendAllEvent(INetworkEvent networkEvent)
        {
            EventPacket eventPacket = new EventPacket(networkEvent);

            SendAll(eventPacket);
        }

        public void Send(IPacket packet, NetPeer peer)
        {
            //if (netManager.ConnectedPeersCount < 1) return;
            NetDataWriter writer = new NetDataWriter();
            writer.PutBytesWithLength(ProtoBufSerializer.Serialize(packet));
            peer.Send(writer, packet.DeliveryMethod);
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
                Logger.Info($"Connecting P2P: {targetEndPoint}");
                _netManager.Connect(targetEndPoint, token);
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            if (PeerServer != null && peer != PeerServer)
            {
                var peerConnectedEvent = new PeerConnected(peer);
                _messageBroker.Publish(this, peerConnectedEvent);
                OnClientConnected?.Invoke(peer);
            }
            Logger.Info($"{_netManager.LocalPort} recieved connection from {peer.EndPoint}");
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            var peerDisconnectedEvent = new PeerDisconnected(peer, disconnectInfo);
            _messageBroker.Publish(this, peerDisconnectedEvent);
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {

        }
        
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            IPacket packet = (IPacket)ProtoBufSerializer.Deserialize(reader.GetBytesWithLength());
            if (m_PacketHandlers.TryGetValue(packet.PacketType, out var handlers))
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
