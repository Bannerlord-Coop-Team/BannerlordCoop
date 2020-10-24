using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Common;
using JetBrains.Annotations;
using LiteNetLib;
using Network;
using Network.Infrastructure;
using Network.Protocol;
using NLog;

namespace Coop.NetImpl.LiteNet
{
    /// <summary>
    ///     Thread to discovery coop servers in LAN.
    /// </summary>
    public class DiscoveryThread
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly object m_AvailableServerLock = new object();

        private readonly HashSet<IPEndPoint> m_AvailableServers = new HashSet<IPEndPoint>();

        private readonly NetworkConfiguration m_Config;

        private readonly NetManager m_NetManager;
        private readonly object m_StopRequestLock = new object();
        [CanBeNull] private FrameLimiter m_FrameLimiter;
        private bool m_IsStopRequest;
        private Thread m_Thread;

        /// <summary>
        ///     Will immediately spawn a new thread and start listing for discovery packets from
        ///     bannerlord coop servers.
        /// </summary>
        /// <param name="config"></param>
        public DiscoveryThread([NotNull] NetworkConfiguration config)
        {
            m_Config = config;

            // Setup network listener
            EventBasedNetListener listener = new EventBasedNetListener();
            listener.NetworkReceiveUnconnectedEvent += (remoteEndPoint, reader, type) =>
            {
                if (type == UnconnectedMessageType.Broadcast)
                {
                    ArraySegment<byte> buffer = reader.GetRemainingBytesSegment();
                    bool isDiscoveryPacket = Discovery.TryDeserialize(new ByteReader(buffer));
                    if (isDiscoveryPacket)
                    {
                        lock (m_AvailableServerLock)
                        {
                            if (m_AvailableServers.Add(remoteEndPoint))
                            {
                                Logger.Info("Server discovered: {peer}", remoteEndPoint);
                            }
                        }
                    }
                }
            };
            m_NetManager = new NetManager(listener)
            {
                BroadcastReceiveEnabled = true,
                ReuseAddress = true
            };
            m_NetManager.Start(m_Config.LanDiscoveryPort);

            // Start thread
            m_Thread = new Thread(Run);
            lock (m_StopRequestLock)
            {
                m_IsStopRequest = false;
            }

            m_Thread.Start();
        }

        /// <summary>
        ///     Retrieve all found server. There is no guarantee that the servers are still available.
        /// </summary>
        public List<IPEndPoint> ServerList
        {
            get
            {
                lock (m_AvailableServers)
                {
                    return m_AvailableServers.ToList();
                }
            }
        }

        ~DiscoveryThread()
        {
            Stop();
            m_NetManager.Stop();
        }

        private void Stop()
        {
            if (m_Thread == null)
            {
                return;
            }

            lock (m_StopRequestLock)
            {
                m_IsStopRequest = false;
            }

            m_Thread.Join();
            m_Thread = null;
        }

        private void Run()
        {
            m_FrameLimiter = new FrameLimiter(TimeSpan.FromMilliseconds(500));
            bool bRunning = true;
            while (bRunning)
            {
                m_NetManager.PollEvents();
                m_FrameLimiter.Throttle();
                lock (m_StopRequestLock)
                {
                    bRunning = !m_IsStopRequest;
                }
            }

            m_FrameLimiter = null;
        }
    }
}
