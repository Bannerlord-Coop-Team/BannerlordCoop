using Coop.Common;
using Coop.Network;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using Coop.Multiplayer;
using Xunit;

namespace Coop.Tests
{
    static class TestUtils
    {
        public static Mock<INetworkConnection> CreateMockConnection()
        {
            Mock<INetworkConnection> connection = new Mock<INetworkConnection>();
            connection.Setup(con => con.FragmentLength).Returns(100);
            connection.Setup(con => con.MaxPackageLength).Returns(100000);
            return connection;
        }

        public static Mock<ISaveData> CreateMockSaveData()
        {
            Mock<ISaveData> saveData = new Mock<ISaveData>();
            saveData = new Mock<ISaveData>();
            saveData.Setup(w => w.Receive(It.IsAny<ArraySegment<byte>>()))
                    .Returns(true);
            saveData.Setup(w => w.SerializeInitialWorldState())
                    .Returns(new byte[0]);
            return saveData;
        }
        public static ServerConfiguration GetTestingConfig()
        {
            ServerConfiguration config = new ServerConfiguration();
            config.lanPort = TestUtils.GetPort();
            config.uiMaxPlayerCount = 4;
            config.uiTickRate = 0;
            return config;
        }
        public static Server StartNewServer()
        {
            Server server = new Server();
            server.Start(GetTestingConfig());
            return server;
        }
        public static void UpdateUntil(Func<bool> condition, List<IUpdateable> updateables)
        {
            TimeSpan totalWaitTime = TimeSpan.Zero;
            TimeSpan waitTimeBetweenTries = TimeSpan.FromMilliseconds(10);
            while (true)
            {
                foreach (var updateable in updateables)
                {
                    updateable.Update(waitTimeBetweenTries);
                }
                if (condition())
                {
                    break;
                }
                else
                {
                    Thread.Sleep(waitTimeBetweenTries);
                    totalWaitTime += waitTimeBetweenTries;
                    // Assert.True(totalWaitTime < TimeSpan.FromMilliseconds(500), "Maximum wait time reached. Abort.");
                }
            }
        }

        private static object UsedPortsLock = new object();
        private static HashSet<int> UsedPorts = new HashSet<int>();
        public static int GetPort()
        {
            lock (UsedPortsLock)
            {
                int iPort = FindAvailablePort(3000);
                UsedPorts.Add(iPort);
                if(iPort == 0)
                {
                    throw new Exception("Could not find any available ports.");
                }
                return iPort;
            }
        }
        private static int FindAvailablePort(int startingPort)
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();

            //getting active connections
            var tcpConnectionPorts = properties.GetActiveTcpConnections()
                                .Where(n => n.LocalEndPoint.Port >= startingPort)
                                .Select(n => n.LocalEndPoint.Port);

            //getting active tcp listners - WCF service listening in tcp
            var tcpListenerPorts = properties.GetActiveTcpListeners()
                                .Where(n => n.Port >= startingPort)
                                .Select(n => n.Port);

            //getting active udp listeners
            var udpListenerPorts = properties.GetActiveUdpListeners()
                                .Where(n => n.Port >= startingPort)
                                .Select(n => n.Port);

            var port = Enumerable.Range(startingPort, ushort.MaxValue)
                .Where(i => !UsedPorts.Contains(i))
                .Where(i => !tcpConnectionPorts.Contains(i))
                .Where(i => !tcpListenerPorts.Contains(i))
                .Where(i => !udpListenerPorts.Contains(i))
                .FirstOrDefault();

            return port;
        }
        public static ArraySegment<byte> MakeRaw(Protocol.EPacket eType, byte[] payload)
        {
            var packet = new Packet(eType, payload);
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            new PacketWriter(packet).Write(writer);
            return stream.ToArray();
        }

        public static ArraySegment<byte> MakeKeepAlive(int iKeepAliveID)
        {
            return MakeRaw(
                Protocol.EPacket.KeepAlive,
                new Protocol.KeepAlive(iKeepAliveID).Serialize());
        }

        public class UpdateThread
        {
            private readonly Action action;
            private readonly TimeSpan targetTickTime;
            public UpdateThread(Action action, TimeSpan targetTickTime)
            {
                this.action = action;
                this.targetTickTime = targetTickTime;
                Start();
            }

            ~UpdateThread()
            {
                Stop();
            }
            public void Start()
            {
                startMainLoop();
            }

            public void Stop()
            {
                stopMainLoop();
            }

            private bool m_bStopRequest = false;
            private readonly object m_StopRequestLock = new object();
            private Thread m_Thread = null;
            private void startMainLoop()
            {
                m_Thread = new Thread(() => run());
                lock (m_StopRequestLock)
                {
                    m_bStopRequest = false;
                }
                m_Thread.Start();
            }

            private void run()
            {
                FrameLimiter frameLimiter = new FrameLimiter(targetTickTime);
                bool bRunning = true;
                while (bRunning)
                {
                    if (bRunning)
                    {
                        action();
                    }

                    frameLimiter.Throttle();
                    lock (m_StopRequestLock)
                    {
                        bRunning = !m_bStopRequest;
                    }
                }
            }

            private void stopMainLoop()
            {
                if (m_Thread == null)
                {
                    return;
                }

                lock (m_StopRequestLock)
                {
                    m_bStopRequest = true;
                }
                m_Thread.Join();
                m_Thread = null;
            }
        }

        public static ArraySegment<byte> MakePersistencePayload(int iPayloadLength)
        {
            byte[] payload = Enumerable.Range(7, iPayloadLength).Select(i => (byte)i).ToArray();
            ByteWriter writer = new ByteWriter();
            writer.Binary.Write(PacketWriter.EncodePacketType(Protocol.EPacket.Persistence));
            writer.Binary.Write(payload);
            return writer.ToArray();
        }
    }
}
