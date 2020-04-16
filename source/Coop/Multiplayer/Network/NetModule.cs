using Coop.Network;
using LiteNetLib;
using System;
using System.Net;
using System.Text;

namespace Coop.Multiplayer.Network
{
    public class NetModule : IServerModule
    {
        private readonly Server m_Server;
        private readonly ServerConfiguration m_Config;
        private readonly NetManager m_wanManager;
        private readonly NetManager m_lanManager;
        public NetModule(Server server)
        {
            if(server == null || server.ActiveConfig == null)
            {
                throw new InvalidServerConfiguration($"Invalid server configuration {m_Server}. Unable to attach NetAdapter.");
            }
            m_Config = server.ActiveConfig;
            if (m_Config.wanAddress == null && m_Config.lanAddress == null)
            {
                throw new InvalidServerConfiguration($"Invalid server configuration {m_Server}. Unable to attach NetAdapter.");
            }

            m_Server = server;
            m_Server.Register(this);

            if(m_Config.wanAddress != null)
            {
                m_wanManager = new NetManager(new NetListenerServer(m_Server));
            }
            if(m_Config.lanAddress != null)
            {
                m_lanManager = new NetManager(new NetListenerServer(m_Server));
            }
            m_SinceLastDiscovery = TimeSpan.Zero;
            m_SinceLastKeepAlive = TimeSpan.Zero;
        }
        ~NetModule()
        {
            m_Server.Unregister(this);
        }
        public void StartListening()
        {
            m_wanManager?.Start(m_Config.wanAddress, IPAddress.IPv6Any, m_Config.wanPort);
            m_lanManager?.Start(m_Config.lanAddress, IPAddress.IPv6Any, m_Config.lanPort);
        }
        public void Stop()
        {
            m_wanManager?.Stop();
            m_lanManager?.Stop();
        }

        public void Tick(TimeSpan frameTime)
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

            if(m_wanManager != null && m_SinceLastKeepAlive > m_Config.wanKeepAliveInterval)
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
