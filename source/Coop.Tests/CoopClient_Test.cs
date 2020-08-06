using System;
using System.Threading;
using System.Threading.Tasks;
using Coop.Mod;
using Coop.NetImpl.LiteNet;
using Moq;
using Network.Infrastructure;
using Xunit;

namespace Coop.Tests
{
    [Collection("Uses LiteNet")]
    [CollectionDefinition("Uses LiteNet", DisableParallelization = true)]
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

        private async Task WaitForClientConnect()
        {
            while (!m_Client.ClientConnected)
            {
                await Task.Delay(m_FrameTime);
                m_Server.Object.Update(m_FrameTime);
                m_Client.Update(m_FrameTime);
            }
        }

        private async Task WaitForTimeout()
        {
            TimeSpan waitedFor = TimeSpan.Zero;
            while (waitedFor < m_DisconnectTimeout + m_FrameTime)
            {
                await Task.Delay(m_FrameTime);
                m_Server.Object.Update(m_FrameTime);
                waitedFor += m_FrameTime;
            }

            // Update the client
            while (m_Client.ClientConnected)
            {
                await Task.Delay(m_FrameTime);
                m_Client.Update(m_DisconnectTimeout);
            }
        }

        [Fact(Timeout = 2000, Skip = "State machine was refactored without adjusting the test. Does not represent the current implementation.")]
        public async Task ClientCanConnect()
        {
            Assert.False(m_Client.ClientConnected);
            ConnectClient();
            await WaitForClientConnect();
            Assert.True(m_Client.ClientConnected);
        }

        [Fact(Timeout = 2000, Skip = "State machine was refactored without adjusting the test. Does not represent the current implementation.")]
        public async Task ClientReconnectsAfterTimeout()
        {
            int iConnectionsCreated = 0;
            int iConnectionsDestroyed = 0;
            m_Client.Session.OnConnectionCreated += connection => { iConnectionsCreated++; };
            m_Client.Session.OnConnectionDestroyed += connection => { iConnectionsDestroyed++; };
            ConnectClient();
            await WaitForClientConnect();
            Assert.True(m_Client.ClientConnected);
            Assert.Equal(1, iConnectionsCreated);
            Assert.Equal(0, iConnectionsDestroyed);

            // Wait for the timeout
            await WaitForTimeout();
            Assert.False(m_Client.ClientConnected);
            Assert.Equal(1, iConnectionsCreated);
            Assert.Equal(1, iConnectionsDestroyed);
            Assert.Null(m_Client.Session.Connection);

            // Wait for the reconnect
            await WaitForClientConnect();
            Assert.True(m_Client.ClientConnected);
            Assert.Equal(2, iConnectionsCreated);
            Assert.Equal(1, iConnectionsDestroyed);
            Assert.NotNull(m_Client.Session.Connection);
        }

        [Fact(Timeout = 2000, Skip = "State machine was refactored without adjusting the test. Does not represent the current implementation.")]
        public async Task ClientTimesOut()
        {
            ConnectClient();
            await WaitForClientConnect();
            Assert.True(m_Client.ClientConnected);

            // Wait for the timeout
            await  WaitForTimeout();
            Assert.False(m_Client.ClientConnected);
        }
    }
}
