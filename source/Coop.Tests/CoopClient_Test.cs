using System;
using System.Threading;
using Coop.Mod;
using Coop.Multiplayer.Network;
using Moq;
using Network.Infrastructure;
using Xunit;

namespace Coop.Tests
{
    public class CoopClient_Test
    {
        public CoopClient_Test()
        {
            ServerConfiguration config = TestUtils.GetTestingConfig();
            config.DisconnectTimeout = m_DisconnectTimeout;
            m_Server = new Mock<Server>(Server.EType.Threaded)
            {
                CallBase = true
            };
            m_Server.Object.Start(config);
            m_NetManagerServer = new LiteNetManagerServer(m_Server.Object, m_WorldData.Object);
            m_NetManagerServer.StartListening();
        }

        private readonly LiteNetManagerServer m_NetManagerServer;
        private readonly Mock<Server> m_Server;
        private readonly Mock<ISaveData> m_WorldData = TestUtils.CreateMockSaveData();
        private readonly CoopClient m_Client = new CoopClient();
        private readonly TimeSpan m_FrameTime = TimeSpan.FromMilliseconds(15);
        private readonly TimeSpan m_DisconnectTimeout = TimeSpan.FromMilliseconds(100);

        private void ConnectClient()
        {
            m_Client.Connect(
                m_Server.Object.ActiveConfig.LanAddress,
                m_Server.Object.ActiveConfig.LanPort);
        }

        private void WaitForClientConnect()
        {
            while (!m_Client.Connected)
            {
                Thread.Sleep(m_FrameTime);
                m_Server.Object.Update(m_FrameTime);
                m_Client.Update(m_FrameTime);
            }
        }

        private void WaitForTimeout()
        {
            TimeSpan waitedFor = TimeSpan.Zero;
            while (waitedFor < m_DisconnectTimeout + m_FrameTime)
            {
                Thread.Sleep(m_FrameTime);
                m_Server.Object.Update(m_FrameTime);
                waitedFor += m_FrameTime;
            }

            // Update the client
            while (m_Client.Connected)
            {
                m_Client.Update(m_DisconnectTimeout);
            }
        }

        [Fact(Timeout = 2000)]
        public void ClientCanConnect()
        {
            Assert.False(m_Client.Connected);
            ConnectClient();
            WaitForClientConnect();
            Assert.True(m_Client.Connected);
        }

        [Fact(Timeout = 2000)]
        public void ClientReconnectsAfterTimeout()
        {
            int iConnectionsCreated = 0;
            int iConnectionsDestroyed = 0;
            m_Client.Session.OnConnectionCreated += connection => { iConnectionsCreated++; };
            m_Client.Session.OnConnectionDestroyed += connection => { iConnectionsDestroyed++; };
            ConnectClient();
            WaitForClientConnect();
            Assert.True(m_Client.Connected);
            Assert.Equal(1, iConnectionsCreated);
            Assert.Equal(0, iConnectionsDestroyed);

            // Wait for the timeout
            WaitForTimeout();
            Assert.False(m_Client.Connected);
            Assert.Equal(1, iConnectionsCreated);
            Assert.Equal(1, iConnectionsDestroyed);
            Assert.Null(m_Client.Session.Connection);

            // Wait for the reconnect
            WaitForClientConnect();
            Assert.True(m_Client.Connected);
            Assert.Equal(2, iConnectionsCreated);
            Assert.Equal(1, iConnectionsDestroyed);
            Assert.NotNull(m_Client.Session.Connection);
        }

        [Fact(Timeout = 2000)]
        public void ClientTimesOut()
        {
            ConnectClient();
            WaitForClientConnect();
            Assert.True(m_Client.Connected);

            // Wait for the timeout
            WaitForTimeout();
            Assert.False(m_Client.Connected);
        }
    }
}
