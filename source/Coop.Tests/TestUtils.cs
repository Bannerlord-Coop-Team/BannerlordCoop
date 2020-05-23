using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using Coop.Common;
using Coop.Multiplayer;
using Coop.Network;
using HarmonyLib;
using Moq;
using NLog;
using NLog.Config;
using NLog.Targets;
using Sync;
using Xunit;

namespace Coop.Tests
{
    internal static class TestUtils
    {
        private static readonly object UsedPortsLock = new object();
        private static readonly HashSet<int> UsedPorts = new HashSet<int>();
        private static bool IsLoggerInitialized;
        private static readonly object LoggerLock = new object();

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
            saveData.Setup(w => w.Receive(It.IsAny<ArraySegment<byte>>())).Returns(true);
            saveData.Setup(w => w.SerializeInitialWorldState()).Returns(new byte[0]);
            return saveData;
        }

        public static void SetupLogger()
        {
            lock (LoggerLock)
            {
                if (!IsLoggerInitialized)
                {
                    LoggingConfiguration config = new LoggingConfiguration();

                    // Targets
                    DebuggerTarget debugOutput = new DebuggerTarget("debugOutput");
                    NLogViewerTarget viewer = new NLogViewerTarget("viewer")
                    {
                        Address = "udp://127.0.0.1:9999",
                        IncludeSourceInfo = true
                    };

                    config.AddRule(LogLevel.Debug, LogLevel.Fatal, debugOutput);
                    config.AddRule(LogLevel.Trace, LogLevel.Fatal, viewer);
                    LogManager.Configuration = config;
                    IsLoggerInitialized = true;
                }
            }
        }

        public static ServerConfiguration GetTestingConfig()
        {
            return new ServerConfiguration
            {
                LanPort = GetPort(),
                MaxPlayerCount = 4,
                TickRate = 0
            };
        }

        public static Server StartNewServer()
        {
            Server server = new Server(Server.EType.Threaded);
            server.Start(GetTestingConfig());
            return server;
        }

        public static void UpdateUntil(Func<bool> condition, List<IUpdateable> updateables)
        {
            TimeSpan totalWaitTime = TimeSpan.Zero;
            TimeSpan waitTimeBetweenTries = TimeSpan.FromMilliseconds(10);
            while (true)
            {
                foreach (IUpdateable updateable in updateables)
                {
                    updateable.Update(waitTimeBetweenTries);
                }

                if (condition())
                {
                    break;
                }

                Thread.Sleep(waitTimeBetweenTries);
                totalWaitTime += waitTimeBetweenTries;
                Assert.True(
                    totalWaitTime < TimeSpan.FromMilliseconds(2000),
                    "Maximum wait time reached. Abort.");
            }
        }

        public static int GetPort()
        {
            lock (UsedPortsLock)
            {
                int iPort = FindAvailablePort(3000);
                UsedPorts.Add(iPort);
                if (iPort == 0)
                {
                    throw new Exception("Could not find any available ports.");
                }

                return iPort;
            }
        }

        private static int FindAvailablePort(int startingPort)
        {
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

            //getting active connections
            IEnumerable<int> tcpConnectionPorts = properties.GetActiveTcpConnections()
                                                            .Where(
                                                                n => n.LocalEndPoint.Port >=
                                                                     startingPort)
                                                            .Select(n => n.LocalEndPoint.Port);

            //getting active tcp listners - WCF service listening in tcp
            IEnumerable<int> tcpListenerPorts = properties.GetActiveTcpListeners()
                                                          .Where(n => n.Port >= startingPort)
                                                          .Select(n => n.Port);

            //getting active udp listeners
            IEnumerable<int> udpListenerPorts = properties.GetActiveUdpListeners()
                                                          .Where(n => n.Port >= startingPort)
                                                          .Select(n => n.Port);

            int port = Enumerable.Range(startingPort, ushort.MaxValue)
                                 .Where(i => !UsedPorts.Contains(i))
                                 .Where(i => !tcpConnectionPorts.Contains(i))
                                 .Where(i => !tcpListenerPorts.Contains(i))
                                 .Where(i => !udpListenerPorts.Contains(i))
                                 .FirstOrDefault();

            return port;
        }

        public static ArraySegment<byte> MakeRaw(Protocol.EPacket eType, byte[] payload)
        {
            Packet packet = new Packet(eType, payload);
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            new PacketWriter(packet).Write(writer);
            return stream.ToArray();
        }

        public static ArraySegment<byte> MakeKeepAlive(int iKeepAliveID)
        {
            return MakeRaw(
                Protocol.EPacket.KeepAlive,
                new Protocol.KeepAlive(iKeepAliveID).Serialize());
        }

        public static ArraySegment<byte> MakePersistencePayload(int iPayloadLength)
        {
            byte[] payload = Enumerable.Range(7, iPayloadLength).Select(i => (byte) i).ToArray();
            ByteWriter writer = new ByteWriter();
            writer.Binary.Write(PacketWriter.EncodePacketType(Protocol.EPacket.Persistence));
            writer.Binary.Write(payload);
            return writer.ToArray();
        }

        public class UpdateThread
        {
            private readonly Action action;
            private readonly object m_StopRequestLock = new object();
            private readonly TimeSpan targetTickTime;

            private bool m_bStopRequest;
            private Thread m_Thread;

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
    }

    public class MockedField<TTarget, TField> : Mock<TTarget>
        where TTarget : class
    {
        private TField m_Latest;

        public MockedField() : base(MockBehavior.Strict)
        {
        }

        public List<TField> ValueHistory { get; set; } = new List<TField>();

        public TField Value
        {
            get => m_Latest;
            set
            {
                ValueHistory.Add(value);
                m_Latest = ValueHistory[^1];
            }
        }
    }

    public class TestableField<TTarget, TField> : SyncField<TTarget, TField>
        where TTarget : class
    {
        public TestableField() : base(
            AccessTools.Field(typeof(MockedField<TTarget, TField>), "m_Latest"))
        {
            Mock = new MockedField<TTarget, TField>();
        }

        public MockedField<TTarget, TField> Mock { get; }
    }
}
