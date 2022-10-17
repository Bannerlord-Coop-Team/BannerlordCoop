using Common;
using Common.Serialization;
using LiteNetLib;
using LiteNetLib.Utils;
using Missions.Config;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Missions.Network
{
    public class LiteNetP2PClient : INatPunchListener, INetEventListener, IUpdateable, IDisposable
    {
        private static Logger m_Logger = LogManager.GetCurrentClassLogger();

        private readonly Guid id = Guid.NewGuid();
        public int ConnectedPeersCount => netManager.ConnectedPeersCount;
        public event Action<NetPeer, DisconnectInfo> OnClientDisconnected;
        public event Action<NetPeer> OnClientConnected;

        NetManager netManager;
        string instance;
        public NetPeer peerServer { get; private set; }
        public int Priority => 2;

        static readonly Dictionary<PacketType, List<IPacketHandler>> m_PacketHandlers = new Dictionary<PacketType, List<IPacketHandler>>();

        NetworkConfiguration networkConfig;
        public LiteNetP2PClient(NetworkConfiguration configuration)
        {
            networkConfig = configuration;

            netManager = new NetManager(this)
            {
                NatPunchEnabled = true,
                DisconnectTimeout = int.MaxValue,
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
            int t = peerServer.TimeSinceLastPacket;
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

            peerServer = netManager.Connect(connectionAddress,
                                            port,
                                            $"{networkConfig.P2PToken}%{id}");

            return peerServer != null;
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
            writer.PutBytesWithLength(CommonSerializer.Serialize(packet, SerializationMethod.ProtoBuf));
            client.Send(writer, packet.DeliveryMethod);
        }

        public void SendAll(IPacket packet)
        {
            //if (netManager.ConnectedPeersCount < 1) return;
            NetDataWriter writer = new NetDataWriter();
            writer.PutBytesWithLength(CommonSerializer.Serialize(packet, SerializationMethod.ProtoBuf));
            netManager.SendToAll(writer, packet.DeliveryMethod);
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            // No requests on client
        }

        static Dictionary<NATType, NatAddressType> natAddressTypeMap = new Dictionary<NATType, NatAddressType>()
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
            if(peerServer != null && peer != peerServer)
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
            IPacket packet = CommonSerializer.Deserialize<IPacket>(reader.GetBytesWithLength(), SerializationMethod.ProtoBuf);
            if (packet.Data == null) throw new NullReferenceException($"{packet.GetType()} is missing data, likely missing a ProtoMember attribute.");
            if (m_PacketHandlers.TryGetValue(packet.PacketType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.HandlePacket(peer, packet);
                    //Task.Factory.StartNew(() => { handler.HandlePacket(peer, packet); });
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

            if(this.instance == instance)
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
