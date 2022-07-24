using System;
using System.Net;
using Common;
using LiteNetLib;
using Network;
using Network.Infrastructure;
using Network.Protocol;

namespace Coop.NetImpl.LiteNet
{
    public class LiteNetManagerServer : IUpdateable
    {
        private readonly ServerConfiguration m_Config;
        private readonly NetManager m_lanManager;
        private readonly Server m_Server;
        private readonly NetManager m_wanManager;

        private int m_iKeepAliveID;
        private TimeSpan m_SinceLastDiscovery;
        private TimeSpan m_SinceLastKeepAlive;

        public LiteNetManagerServer(Server server)
        {
            if (server?.ActiveConfig == null)
            {
                throw new ArgumentNullException();
            }

            m_Config = server.ActiveConfig;
            if (m_Config.NetworkConfiguration.WanAddress == null && m_Config.NetworkConfiguration.LanAddress == null)
            {
                throw new InvalidServerConfiguration(
                    $"Invalid server configuration {m_Server}. Unable to attach NetAdapter.");
            }

            m_Server = server;
            if (m_Config.NetworkConfiguration.WanAddress != null)
            {
                m_wanManager = CreateNetManager();
            }

            if (m_Config.NetworkConfiguration.LanAddress != null)
            {
                m_lanManager = CreateNetManager();
            }

            m_SinceLastDiscovery = TimeSpan.Zero;
            m_SinceLastKeepAlive = TimeSpan.Zero;
        }

        public void Update(TimeSpan frameTime)
        {
            m_SinceLastDiscovery += frameTime;
            m_SinceLastKeepAlive += frameTime;

            m_lanManager?.PollEvents();
            m_wanManager?.PollEvents();

            if (m_lanManager != null && m_SinceLastDiscovery > m_Config.NetworkConfiguration.LanDiscoveryInterval)
            {
                m_lanManager.SendBroadcast(Network.Protocol.Discovery.GetPayload(), m_Config.NetworkConfiguration.LanDiscoveryPort);
                m_SinceLastDiscovery = TimeSpan.Zero;
            }

            if (m_SinceLastKeepAlive > m_Config.NetworkConfiguration.KeepAliveInterval)
            {
                m_Server.SendToAll(
                    new Packet(EPacket.KeepAlive, new KeepAlive(++m_iKeepAliveID).Serialize()));
                m_SinceLastKeepAlive = TimeSpan.Zero;
            }
        }
        public int Priority { get; } = UpdatePriority.ServerThread.PollNetwork;

        private NetManager CreateNetManager()
        {
            return NetManagerFactory.Create(
                new LiteNetListenerServer(m_Server),
                m_Config.NetworkConfiguration);
        }

        public void StartListening()
        {
            m_wanManager?.Start(m_Config.NetworkConfiguration.WanAddress, IPAddress.IPv6Any, m_Config.NetworkConfiguration.WanPort);
            m_lanManager?.Start(m_Config.NetworkConfiguration.LanAddress, IPAddress.IPv6Any, m_Config.NetworkConfiguration.LanPort);
            m_Server.Updateables.Add(this);
        }

        public void Stop()
        {
            m_wanManager?.Stop();
            m_lanManager?.Stop();
            m_Server.Updateables.Remove(this);
        }
    }
}
