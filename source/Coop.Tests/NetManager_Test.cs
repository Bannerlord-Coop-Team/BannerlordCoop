using Coop.Common;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using Coop.Network;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace Coop.Tests
{
    public class NetManager_Test
    {
        private readonly Server m_Server;
        private readonly NetManagerServer m_NetManagerServer;
        public NetManager_Test()
        {
            m_Server = TestUtils.StartNewServer();
            m_NetManagerServer = new NetManagerServer(m_Server);
            m_NetManagerServer.StartListening();
        }

        private class Client
        {
            public readonly ClientSession Session;
            public readonly NetManagerClient Manager;

            public Client()
            {
                Session = new ClientSession();
                Manager = new NetManagerClient(Session);
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
