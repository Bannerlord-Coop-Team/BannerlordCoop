﻿using System.Collections.Generic;
using Common;
using Coop.NetImpl.LiteNet;
using Moq;
using Network.Infrastructure;
using Xunit;

namespace Coop.Tests.Network
{
    [Collection("Uses LiteNet")]
    [CollectionDefinition("Uses LiteNet", DisableParallelization = true)]
    public class NetManager_Test
    {
        public NetManager_Test()
        {
            m_Server = TestUtils.StartNewServer();
            m_NetManagerServer = new LiteNetManagerServer(m_Server, Mock.Of<ISaveData>());
            m_NetManagerServer.StartListening();
            CompatibilityInfo.ModuleProvider = new ModuleInfoProviderMock();
        }

        private readonly Server m_Server;
        private readonly LiteNetManagerServer m_NetManagerServer;

        private class Client
        {
            public readonly LiteNetManagerClient Manager;
            public readonly GameSession Session;

            public Client()
            {
                Session = new GameSession(Mock.Of<ISaveData>());
                Manager = new LiteNetManagerClient(Session, new ClientConfiguration());
            }
        }

        [Fact]
        private void ClientCanConnect()
        {
            Client client = new Client();
            client.Manager.Connect(m_Server.ActiveConfig.NetworkConfiguration.LanAddress, m_Server.ActiveConfig.NetworkConfiguration.LanPort);
            TestUtils.UpdateUntil(
                () => client.Session.Connection != null,
                new List<IUpdateable>
                {
                    client.Manager
                });
        }

        [Fact]
        private void ClientCanDisconnect()
        {
            Client client = new Client();
            client.Manager.Connect(m_Server.ActiveConfig.NetworkConfiguration.LanAddress, m_Server.ActiveConfig.NetworkConfiguration.LanPort);
            TestUtils.UpdateUntil(
                () => client.Session.Connection != null,
                new List<IUpdateable>
                {
                    client.Manager
                });
            Assert.NotNull(client.Session.Connection);
            Assert.True(client.Manager.Connected);

            client.Manager.Disconnect(EDisconnectReason.ClientLeft);
            TestUtils.UpdateUntil(
                () => client.Session.Connection == null,
                new List<IUpdateable>
                {
                    client.Manager
                });
            Assert.Null(client.Session.Connection);
            Assert.False(client.Manager.Connected);
        }
    }
}
