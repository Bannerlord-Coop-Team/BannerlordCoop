using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Common;
using Coop.NetImpl.LiteNet;
using LiteNetLib;
using Moq;
using Network.Infrastructure;
using Xunit;

namespace Coop.Tests.Network
{
    [Collection("Uses LiteNet")]
    [CollectionDefinition("Uses LiteNet", DisableParallelization = true)]
    public class NetListener_test
    {
        public NetListener_test()
        {
            // Setup server mock
            m_Server = new Mock<Server>(Server.EType.Threaded);
            m_ListenerServer = new LiteNetListenerServer(m_Server.Object, Mock.Of<ISaveData>());
            m_ServerSideConnected = new List<ConnectionBase>();
            m_ServerSideDisconnects = new List<(ConnectionBase, EDisconnectReason)>();
            m_Server.Setup(server => server.CanPlayerJoin()).Returns(true);
            m_Server.Setup(server => server.Connected(It.IsAny<ConnectionServer>()))
                    .Callback((ConnectionBase con) => m_ServerSideConnected.Add(con));
            m_Server
                .Setup(
                    server => server.Disconnected(
                        It.IsAny<ConnectionServer>(),
                        It.IsAny<EDisconnectReason>()))
                .Callback<ConnectionBase, EDisconnectReason>(
                    (con, eReason) => m_ServerSideDisconnects.Add((con, eReason)));

            // Start the server
            m_iServerPort = TestUtils.GetPort();
            m_NetManagerServer = new NetManager(m_ListenerServer);
            m_NetManagerServer.Start(
                IPAddress.Parse(m_sServerIP),
                IPAddress.IPv6Any,
                m_iServerPort);
            CompatibilityInfo.ModuleProvider = new ModuleInfoProviderMock();
        }

        private const string m_sServerIP = "127.0.0.1";
        private readonly int m_iServerPort;

        private class Client
        {
            public readonly LiteNetListenerClient Listener;

            private readonly int m_iPort;
            public readonly NetManager Manager;
            public readonly GameSession Session;
            public NetPeer PeerServer;

            public Client(int iPort)
            {
                m_iPort = iPort;
                Session = new GameSession(Mock.Of<ISaveData>());
                Listener = new LiteNetListenerClient(Session);
                Manager = new NetManager(Listener);
                Manager.Start();
                Manager.ReconnectDelay = 25;
                Manager.MaxConnectAttempts = 10;
            }

            public bool Connected
            {
                get
                {
                    if (PeerServer == null)
                    {
                        return false;
                    }

                    return PeerServer.ConnectionState.HasFlag(ConnectionState.Connected);
                }
            }

            ~Client()
            {
                if (Connected)
                {
                    Disconnect();
                }
            }

            public void ConnectToServer()
            {
                PeerServer = Manager.Connect(m_sServerIP, m_iPort, "");
            }

            public void Disconnect()
            {
                Manager.DisconnectPeer(
                    PeerServer,
                    new[] {Convert.ToByte(EDisconnectReason.ClientLeft)});
            }
        }

        private readonly Mock<Server> m_Server;
        private readonly List<ConnectionBase> m_ServerSideConnected;
        private readonly List<(ConnectionBase, EDisconnectReason)> m_ServerSideDisconnects;
        private readonly LiteNetListenerServer m_ListenerServer;
        private readonly NetManager m_NetManagerServer;

        private void PollUntil(Func<bool> condition, List<Client> clients)
        {
            PollUntil(
                condition,
                clients.Select(client => client.Manager).Prepend(m_NetManagerServer).ToList());
        }

        private void PollUntil(Func<bool> condition, List<NetManager> managers)
        {
            TimeSpan totalWaitTime = TimeSpan.Zero;
            TimeSpan waitTimeBetweenTries = TimeSpan.FromMilliseconds(10);
            while (true)
            {
                foreach (NetManager manager in managers)
                {
                    manager.PollEvents();
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

        [Fact]
        private void ClientCantJoinWhenServerIsFull()
        {
            // Setup server to accept enough clients
            const int iNumberOfClients = 4;
            m_Server.Setup(server => server.CanPlayerJoin())
                    .Returns(() => m_ServerSideConnected.Count < iNumberOfClients);

            // Create clients
            List<Client> clients = new List<Client>();
            for (int i = 0; i < iNumberOfClients; ++i)
            {
                Client client = new Client(m_iServerPort);
                client.ConnectToServer();
                clients.Add(client);
            }

            // Wait until all clients are connected to the server
            PollUntil(() => clients.All(client => client.Connected), clients);
            Assert.Equal(iNumberOfClients, m_ServerSideConnected.Count);
            Assert.Equal(iNumberOfClients, m_NetManagerServer.ConnectedPeerList.Count);

            // Since the server is full, an additional client will be rejected
            EventBasedNetListener rejectedClientListener = new EventBasedNetListener();
            NetManager rejectedClientNetManager = new NetManager(rejectedClientListener);
            bool bRejected = false;
            EDisconnectReason eReason = EDisconnectReason.Unknown;
            rejectedClientListener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                bRejected = true;
                Assert.True(disconnectInfo.AdditionalData.AvailableBytes == 1);
                eReason = (EDisconnectReason) disconnectInfo.AdditionalData.GetByte();
            };
            rejectedClientNetManager.Start();
            NetPeer rejectedServerPeer =
                rejectedClientNetManager.Connect(m_sServerIP, m_iServerPort, "");
            PollUntil(
                () => bRejected,
                new List<NetManager>
                {
                    rejectedClientNetManager,
                    m_NetManagerServer
                });
            Assert.Equal(EDisconnectReason.ServerIsFull, eReason);
            Assert.Equal(iNumberOfClients, m_ServerSideConnected.Count);
            Assert.Equal(iNumberOfClients, m_NetManagerServer.ConnectedPeerList.Count);
            Assert.True(rejectedServerPeer.ConnectionState.HasFlag(ConnectionState.Disconnected));
        }

        [Fact]
        private void TestClientServerConnection()
        {
            // Connect client to server
            List<Client> clients = new List<Client>();
            clients.Add(new Client(m_iServerPort));
            clients[0].ConnectToServer();
            PollUntil(() => clients[0].Connected, clients);

            // Verify that the connection has been established.
            m_Server.Verify(s => s.Connected(It.IsAny<ConnectionServer>()), Times.Once());
            Assert.Single(m_ServerSideConnected);
            m_Server.Verify(
                s => s.Disconnected(It.IsAny<ConnectionServer>(), It.IsAny<EDisconnectReason>()),
                Times.Never());
            Assert.Empty(m_ServerSideDisconnects);
            Assert.NotNull(clients[0].Session.Connection);

            // Disconnect
            clients[0].Disconnect();
            PollUntil(() => !clients[0].Connected && m_ServerSideDisconnects.Count == 1, clients);

            // Verify that no connections are open
            m_Server.Verify(s => s.Connected(It.IsAny<ConnectionServer>()), Times.Once());
            Assert.Single(m_ServerSideConnected);
            m_Server.Verify(
                s => s.Disconnected(It.IsAny<ConnectionServer>(), It.IsAny<EDisconnectReason>()),
                Times.Once());
            Assert.Single(m_ServerSideDisconnects);
            Assert.Equal(m_ServerSideConnected[0], m_ServerSideDisconnects[0].Item1);
            Assert.Equal(EDisconnectReason.ClientLeft, m_ServerSideDisconnects[0].Item2);
            Assert.Null(clients[0].Session.Connection);
        }

        [Fact]
        private void TestMultipleClients()
        {
            const int iNumberOfClients = 8;

            // Create clients
            List<Client> clients = new List<Client>();
            for (int i = 0; i < iNumberOfClients; ++i)
            {
                Client client = new Client(m_iServerPort);
                client.ConnectToServer();
                clients.Add(client);
                PollUntil(() => client.Connected, clients);
            }

            // Wait until all clients are connected to the server
            
            Assert.Equal(iNumberOfClients, m_ServerSideConnected.Count);
            Assert.Equal(iNumberOfClients, m_NetManagerServer.ConnectedPeerList.Count);
        }
    }
}
