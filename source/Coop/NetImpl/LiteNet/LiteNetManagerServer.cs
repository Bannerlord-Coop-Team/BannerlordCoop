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

        public LiteNetManagerServer(Server server, ISaveData worldData)
        {
            if (server?.ActiveConfig == null || worldData == null)
            {
                throw new ArgumentNullException();
            }

            m_Config = server.ActiveConfig;
            if (m_Config.WanAddress == null && m_Config.LanAddress == null)
            {
                throw new InvalidServerConfiguration(
                    $"Invalid server configuration {m_Server}. Unable to attach NetAdapter.");
            }

            m_Server = server;
            if (m_Config.WanAddress != null)
            {
                m_wanManager = CreateNetManager(worldData);
            }

            if (m_Config.LanAddress != null)
            {
                m_lanManager = CreateNetManager(worldData);
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

            if (m_lanManager != null && m_SinceLastDiscovery > m_Config.LanDiscoveryInterval)
            {
                // TODO: LiteNetLib removed SendDiscoveryRequest in 0.9. Replacement?
                // m_lanManager.SendDiscoveryRequest(Encoding.UTF8.GetBytes(Globals.LanDiscoveryString), Globals.LanDiscoveryPort);
                m_SinceLastDiscovery = TimeSpan.Zero;
            }

            if (m_SinceLastKeepAlive > m_Config.KeepAliveInterval)
            {
                m_Server.SendToAll(
                    new Packet(EPacket.KeepAlive, new KeepAlive(++m_iKeepAliveID).Serialize()));
                m_SinceLastKeepAlive = TimeSpan.Zero;
            }
        }

        private NetManager CreateNetManager(ISaveData worldData)
        {
            return new NetManager(new LiteNetListenerServer(m_Server, worldData))
            {
                DisconnectTimeout = (int) m_Config.DisconnectTimeout.TotalMilliseconds
            };
        }

        public void StartListening()
        {
            m_wanManager?.Start(m_Config.WanAddress, IPAddress.IPv6Any, m_Config.WanPort);
            m_lanManager?.Start(m_Config.LanAddress, IPAddress.IPv6Any, m_Config.LanPort);
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
