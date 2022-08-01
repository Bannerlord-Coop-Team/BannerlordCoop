using Common;
using Common.Serialization;
using LiteNetLib;
using LiteNetLib.Utils;
using Network.Infrastructure;
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

namespace Coop.NetImpl.LiteNet
{
    public class LiteNetP2PClient : INatPunchListener, INetEventListener, IUpdateable, IDisposable
    {
        private static Logger m_Logger = LogManager.GetCurrentClassLogger();
        public int ConnectedPeersCount => netManager.ConnectedPeersCount;
        public event Action<NetPeer, DisconnectInfo> OnClientDisconnected;

        NetManager netManager;
        string instance;
        public NetPeer peerServer { get; private set; }
        public int Priority => 2;

        static readonly Dictionary<PacketType, List<IPacketHandler>> m_PacketHandlers = new Dictionary<PacketType, List<IPacketHandler>>();

        NetworkConfiguration networkConfig;
        public LiteNetP2PClient(NetworkConfiguration configuration)
        {
            this.networkConfig = configuration;

            netManager = new NetManager(this)
            {
                IPv6Enabled = IPv6Mode.DualMode,
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
            if(m_PacketHandlers.TryGetValue(handler.PacketType, out List<IPacketHandler> list))
            {
                list.Remove(handler);
            }
        }

        public void Update(TimeSpan frameTime)
        {
            netManager.PollEvents();
            netManager.NatPunchModule.PollEvents();
        }

        public bool ConnectToP2PServer(string instance)
        {
            this.instance = instance;

            TryPunch(instance);

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
                                            networkConfig.P2PToken);

            return peerServer != null;
        }

        private void TryPunch(string instance)
        {
            if(networkConfig.NATType == NATType.Internal)
            {
                netManager.NatPunchModule.SendNatIntroduceRequest(networkConfig.LanAddress.ToString(), networkConfig.LanPort, instance);
            }
            else if(networkConfig.NATType == NATType.External)
            {
                netManager.NatPunchModule.SendNatIntroduceRequest(networkConfig.WanAddress.ToString(), networkConfig.WanPort, instance);
            }
        }
            

        public void Stop()
        {
            netManager.DisconnectAll();
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
            m_Logger.Info($"{this.netManager.LocalPort} recieved connection from {peer.EndPoint}");
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
            if(m_PacketHandlers.TryGetValue(packet.PacketType, out var handlers))
            {
                foreach(var handler in handlers)
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
            request.AcceptIfKey(instance);
        }
    }
}
