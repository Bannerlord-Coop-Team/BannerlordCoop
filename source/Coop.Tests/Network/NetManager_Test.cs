using Coop.Common;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using Coop.Network;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests
{
    public class NetManager_Test
    {
        private readonly Server m_Server;
        private readonly LiteNetManagerServer m_NetManagerServer;
        public NetManager_Test()
        {
            m_Server = TestUtils.StartNewServer();
            m_NetManagerServer = new LiteNetManagerServer(m_Server, Mock.Of<ISaveData>());
            m_NetManagerServer.StartListening();
        }

        private class Client
        {
            public readonly GameSession Session;
            public readonly LiteNetManagerClient Manager;

            public Client()
            {
                Session = new GameSession(Mock.Of<ISaveData>());
                Manager = new LiteNetManagerClient(Session);
            }
        }

        [Fact]
        void ClientCanConnect()
        {
            Client client = new Client();
            client.Manager.Connect(m_Server.ActiveConfig.lanAddress.ToString(), m_Server.ActiveConfig.lanPort);
            TestUtils.UpdateUntil(() => client.Session.Connection != null, new List<IUpdateable>() { client.Manager });
        }
        [Fact]
        void ClientCanDisconnect()
        {
            Client client = new Client();
            client.Manager.Connect(m_Server.ActiveConfig.lanAddress.ToString(), m_Server.ActiveConfig.lanPort);
            TestUtils.UpdateUntil(() => client.Session.Connection != null, new List<IUpdateable>() { client.Manager });
            Assert.NotNull(client.Session.Connection);
            Assert.True(client.Manager.Connected);

            client.Manager.Disconnect(EDisconnectReason.ClientLeft);
            TestUtils.UpdateUntil(() => client.Session.Connection == null, new List<IUpdateable>() { client.Manager });
            Assert.Null(client.Session.Connection);
            Assert.False(client.Manager.Connected);
        }
    }
}
