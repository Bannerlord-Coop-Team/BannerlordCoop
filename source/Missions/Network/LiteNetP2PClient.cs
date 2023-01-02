using Common;
using Common.Serialization;
using IntroServer.Config;
using IntroServer.Data;
using IntroServer.Server;
using LiteNetLib;
using LiteNetLib.Utils;
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
        private static readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

        private readonly Guid id = Guid.NewGuid();
        private readonly BatchLogger<PacketType> _batchLogger = new BatchLogger<PacketType>(LogLevel.Trace);
        public int ConnectedPeersCount => netManager.ConnectedPeersCount;
        public event Action<NetPeer, DisconnectInfo> OnClientDisconnected;
        public event Action<NetPeer> OnClientConnected;

        public NetPeer PeerServer { get; private set; }
        public int Priority => 2;

        private readonly NetManager netManager;
        private string instance;
        

        private static readonly Dictionary<PacketType, List<IPacketHandler>> m_PacketHandlers = new Dictionary<PacketType, List<IPacketHandler>>();
        private readonly NetworkConfiguration networkConfig;
        private readonly Version _version = typeof(MissionTestServer).Assembly.GetName().Version;
        public LiteNetP2PClient(NetworkConfiguration configuration)
        {
            networkConfig = configuration;

            netManager = new NetManager(this)
            {
                NatPunchEnabled = true,
            };

            netManager.NatPunchModule.Init(this);

            netManager.Start();
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
            if (m_PacketHandlers.ContainsKey(handler.PacketType))
            {
                m_PacketHandlers[handler.PacketType].Add(handler);
            }
            else
            {
                m_PacketHandlers.Add(handler.PacketType, new List<IPacketHandler> { handler });
            }
        }

        public void RemoveHandler(IPacketHandler handler)
        {
            if (m_PacketHandlers.TryGetValue(handler.PacketType, out List<IPacketHandler> list))
            {
                list.Remove(handler);
            }
        }

        public void Update(TimeSpan frameTime)
        {
            netManager.PollEvents();
            netManager.NatPunchModule.PollEvents();
        }

        public bool ConnectToP2PServer()
        {
            string connectionAddress;
            int port;
            if (networkConfig.NATType == NATType.Internal)
            {
                connectionAddress = networkConfig.LanAddress.ToString();
                port = networkConfig.LanPort;
            }
            else
            {
                connectionAddress = networkConfig.WanAddress.ToString();
                port = networkConfig.WanPort;
            }

            ClientInfo clientInfo = new ClientInfo(
                id,
                _version);

            PeerServer = netManager.Connect(connectionAddress,
                                            port,
                                            clientInfo.ToString());

            return PeerServer != null;
        }

        public void NatPunch(string instance)
        {
            this.instance = instance;
            TryPunch(instance);
        }

        private void TryPunch(string instance)
        {
            string token = $"{instance}%{id}";
            if (networkConfig.NATType == NATType.Internal)
            {
                netManager.NatPunchModule.SendNatIntroduceRequest(networkConfig.LanAddress.ToString(), networkConfig.LanPort, token);
            }
            else if (networkConfig.NATType == NATType.External)
            {
                netManager.NatPunchModule.SendNatIntroduceRequest(networkConfig.WanAddress.ToString(), networkConfig.WanPort, token);
            }
        }

        public void Stop()
        {
            netManager.DisconnectAll();
            netManager.Stop();
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
            netManager.SendToAll(writer, packet.DeliveryMethod);
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
            if (type == natAddressTypeMap[networkConfig.NATType])
            {
                m_Logger.Info($"Connecting P2P: {targetEndPoint}");
                netManager.Connect(targetEndPoint, token);
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            if (PeerServer != null && peer != PeerServer)
            {
                OnClientConnected?.Invoke(peer);
            }
            m_Logger.Info($"{netManager.LocalPort} recieved connection from {peer.EndPoint}");
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            foreach (var handlers in m_PacketHandlers.Values)
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
