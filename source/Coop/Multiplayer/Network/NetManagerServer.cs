using Coop.Common;
using Coop.Network;
using LiteNetLib;
using System;
using System.Net;
using System.Text;

namespace Coop.Multiplayer.Network
{
    public class NetManagerServer : IUpdateable
    {
        private readonly Server m_Server;
        private readonly IWorldData m_WorldData;
        private readonly ServerConfiguration m_Config;
        private readonly NetManager m_wanManager;
        private readonly NetManager m_lanManager;
        public NetManagerServer(Server server, IWorldData worldData)
        {
            if(server == null || server.ActiveConfig == null || worldData == null)
            {
                throw new ArgumentNullException();
            }
            m_Config = server.ActiveConfig;
            if (m_Config.wanAddress == null && m_Config.lanAddress == null)
            {
                throw new InvalidServerConfiguration($"Invalid server configuration {m_Server}. Unable to attach NetAdapter.");
            }
            m_WorldData = worldData;

            m_Server = server;
            if(m_Config.wanAddress != null)
            {
                m_wanManager = new NetManager(new NetListenerServer(m_Server, m_WorldData));
            }
            if(m_Config.lanAddress != null)
            {
                m_lanManager = new NetManager(new NetListenerServer(m_Server, m_WorldData));
            }
            m_SinceLastDiscovery = TimeSpan.Zero;
            m_SinceLastKeepAlive = TimeSpan.Zero;

            
        }
        public void StartListening()
        {
            m_wanManager?.Start(m_Config.wanAddress, IPAddress.IPv6Any, m_Config.wanPort);
            m_lanManager?.Start(m_Config.lanAddress, IPAddress.IPv6Any, m_Config.lanPort);
            m_Server.Updateables.Add(this);
        }
        public void Stop()
        {
            m_wanManager?.Stop();
            m_lanManager?.Stop();
            m_Server.Updateables.Remove(this);
        }

        public void Update(TimeSpan frameTime)
        {
            m_SinceLastDiscovery += frameTime;
            m_SinceLastKeepAlive += frameTime;

            m_lanManager?.PollEvents();
            m_wanManager?.PollEvents();

            if(m_lanManager != null && m_SinceLastDiscovery > m_Config.lanDiscoveryInterval)
            {
                m_lanManager.SendDiscoveryRequest(Encoding.UTF8.GetBytes(Globals.LanDiscoveryString), Globals.LanDiscoveryPort);
                m_SinceLastDiscovery = TimeSpan.Zero;
            }

            if(m_SinceLastKeepAlive > m_Config.keepAliveInterval)
            {
                m_Server.SendToAll(new Packet(Protocol.EPacket.Server_KeepAlive, new Protocol.Server_KeepAlive(++m_iKeepAliveID).Serialize()));
                m_SinceLastKeepAlive = TimeSpan.Zero;
            }
        }

        private int m_iKeepAliveID = 0;
        private TimeSpan m_SinceLastDiscovery;
        private TimeSpan m_SinceLastKeepAlive;
    }
}
