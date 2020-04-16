using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using Coop.Network;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Xunit;

namespace Coop.Tests
{
    public class NetListener_test
    {
        private const string m_sServerIP = "127.0.0.1";
        private readonly int m_iServerPort;
        private class Client
        {
            public readonly ClientSession Session;
            public readonly NetListenerClient Listener;
            public readonly NetManager Manager;
            public NetPeer PeerServer;

            private readonly int m_iPort;
            public Client(int iPort)
            {
                m_iPort = iPort;
                Session = new ClientSession();
                Listener = new NetListenerClient(Session);
                Manager = new NetManager(Listener);
                Manager.Start();
                Manager.ReconnectDelay = 0;
                Manager.MaxConnectAttempts = 1;
            }
            ~Client()
            {
                if (Connected)
                {
                    Disconnect();
                }
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
            public void ConnectToServer()
            {
                PeerServer = Manager.Connect(m_sServerIP, m_iPort, "");
            }

            public void Disconnect()
            {
                Manager.DisconnectPeer(PeerServer, new byte[] { Convert.ToByte(EDisconnectReason.ClientLeft) });
            }
        }

        private readonly Mock<Server> m_Server;
        private readonly List<ConnectionBase> m_ServerSideConnected;
        private readonly List<(ConnectionBase, EDisconnectReason)> m_ServerSideDisconnects;
        private readonly NetListenerServer m_ListenerServer;
        private readonly NetManager m_NetManagerServer;
        public NetListener_test()
        {
            // Setup server mock
            m_Server = new Mock<Server>();
            m_ListenerServer = new NetListenerServer(m_Server.Object);
            m_ServerSideConnected = new List<ConnectionBase>();
            m_ServerSideDisconnects = new List<(ConnectionBase, EDisconnectReason)>();
            m_Server.Setup(server => server.CanPlayerJoin()).Returns(true);
            m_Server.Setup(server => server.OnConnected(It.IsAny<ConnectionBase>()))
                .Callback((ConnectionBase con) => m_ServerSideConnected.Add(con));
            m_Server.Setup(server => server.OnDisconnected(It.IsAny<ConnectionBase>(), It.IsAny<EDisconnectReason>()))
                .Callback<ConnectionBase, EDisconnectReason>((con, eReason) => m_ServerSideDisconnects.Add((con, eReason)));

            // Start the server
            m_iServerPort = TestUtils.GetPort();
            m_NetManagerServer = new NetManager(m_ListenerServer);
            m_NetManagerServer.Start(IPAddress.Parse(m_sServerIP), IPAddress.IPv6Any, m_iServerPort);
        }

        void PollUntil(Func<bool> condition, List<Client> clients)
        {
            PollUntil(condition, clients.Select(client => client.Manager).Prepend(m_NetManagerServer).ToList());
        }

        void PollUntil(Func<bool> condition, List<NetManager> managers)
        {
            TimeSpan totalWaitTime = TimeSpan.Zero;
            TimeSpan waitTimeBetweenTries = TimeSpan.FromMilliseconds(10);
            while (true)
            {
                foreach (var manager in managers)
                {
                    manager.PollEvents();
                }
                if (condition())
                {
                    break;
                }
                else
                {
                    Thread.Sleep(waitTimeBetweenTries);
                    totalWaitTime += waitTimeBetweenTries;
                    Assert.True(totalWaitTime < TimeSpan.FromMilliseconds(500), "Maximum wait time reached. Abort.");
                }
            }
        }

        [Fact]
        void TestClientServerConnection()
        {
            // Connect client to server
            List<Client> clients = new List<Client>();
            clients.Add(new Client(m_iServerPort));
            clients[0].ConnectToServer();
            PollUntil(() => clients[0].Connected, clients);

            // Verify that the connection has been established.
            m_Server.Verify(s => s.OnConnected(It.IsAny<ConnectionBase>()), Times.Once());
            Assert.Single(m_ServerSideConnected);
            m_Server.Verify(s => s.OnDisconnected(It.IsAny<ConnectionBase>(), It.IsAny<EDisconnectReason>()), Times.Never());
            Assert.Empty(m_ServerSideDisconnects);
            Assert.NotNull(clients[0].Session.Connection);

            // Disconnect
            clients[0].Disconnect();
            PollUntil(() => !clients[0].Connected && m_ServerSideDisconnects.Count == 1, clients);

            // Verify that no connections are open
            m_Server.Verify(s => s.OnConnected(It.IsAny<ConnectionBase>()), Times.Once());
            Assert.Single(m_ServerSideConnected);
            m_Server.Verify(s => s.OnDisconnected(It.IsAny<ConnectionBase>(), It.IsAny<EDisconnectReason>()), Times.Once());
            Assert.Single(m_ServerSideDisconnects);
            Assert.Equal(m_ServerSideConnected[0], m_ServerSideDisconnects[0].Item1);
            Assert.Equal(EDisconnectReason.ClientLeft, m_ServerSideDisconnects[0].Item2);
            Assert.Null(clients[0].Session.Connection);
        }

        [Fact]
        void TestMultipleClients()
        {
            const int iNumberOfClients = 8;

            // Create clients
            List<Client> clients = new List<Client>();
            for (int i = 0; i < iNumberOfClients; ++i)
            {
                var client = new Client(m_iServerPort);
                client.ConnectToServer();
                clients.Add(client);
            }

            // Wait until all clients are connected to the server
            PollUntil(() => clients.All(client => client.Connected), clients);
            Assert.Equal(iNumberOfClients, m_ServerSideConnected.Count);
            Assert.Equal(iNumberOfClients, m_NetManagerServer.ConnectedPeerList.Count);
        }

        [Fact]
        void ClientCantJoinWhenServerIsFull()
        {
            // Setup server to accept enough clients
            const int iNumberOfClients = 4;
            m_Server.Setup(server => server.CanPlayerJoin()).Returns(() => m_ServerSideConnected.Count < iNumberOfClients);

            // Create clients
            List<Client> clients = new List<Client>();
            for (int i = 0; i < iNumberOfClients; ++i)
            {
                var client = new Client(m_iServerPort);
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
                eReason = (EDisconnectReason)disconnectInfo.AdditionalData.GetByte();
            };
            rejectedClientNetManager.Start();
            NetPeer rejectedServerPeer = rejectedClientNetManager.Connect(m_sServerIP, m_iServerPort, "");
            PollUntil(() => bRejected, new List<NetManager>() { rejectedClientNetManager, m_NetManagerServer });
            Assert.Equal(EDisconnectReason.ServerIsFull, eReason);
            Assert.Equal(iNumberOfClients, m_ServerSideConnected.Count);
            Assert.Equal(iNumberOfClients, m_NetManagerServer.ConnectedPeerList.Count);
            Assert.True(rejectedServerPeer.ConnectionState.HasFlag(ConnectionState.Disconnected));
        }
    }
}
