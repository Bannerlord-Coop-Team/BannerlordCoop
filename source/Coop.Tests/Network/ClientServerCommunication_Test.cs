using System;
using System.Collections.Generic;
using Common;
using Coop.NetImpl.LiteNet;
using Moq;
using Network.Infrastructure;
using Network.Protocol;
using Xunit;

namespace Coop.Tests.Network
{
    public class ClientServerCommunication_Test
    {
        private readonly TimeSpan m_keepAliveInterval = TimeSpan.FromMilliseconds(50);
        private readonly LiteNetManagerServer m_NetManagerServer;
        private readonly Mock<Server> m_Server;
        private readonly Mock<ISaveData> m_WorldData = TestUtils.CreateMockSaveData();

        public ClientServerCommunication_Test()
        {
            TestUtils.SetupLogger();
            ServerConfiguration config = TestUtils.GetTestingConfig();
            config.KeepAliveInterval = m_keepAliveInterval;
            m_Server = new Mock<Server>(Server.EType.Threaded)
            {
                CallBase = true
            };
            m_Server.Object.Start(config);
            m_NetManagerServer = new LiteNetManagerServer(m_Server.Object, m_WorldData.Object);
            m_NetManagerServer.StartListening();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        private void ClientSendOrder(bool bExchangeWorldData)
        {
            m_WorldData.Setup(d => d.RequiresInitialWorldData).Returns(bExchangeWorldData);

            List<(EConnectionState, EPacket)> expectedReceiveOrderOnServer =
                new List<(EConnectionState, EPacket)>();
            expectedReceiveOrderOnServer.Add(
                (EConnectionState.ServerAwaitingClient, EPacket.Client_Hello));
            expectedReceiveOrderOnServer.Add(
                (EConnectionState.ServerAwaitingClient, EPacket.Client_Info));
            if (bExchangeWorldData)
            {
                expectedReceiveOrderOnServer.Add(
                    (EConnectionState.ServerJoining, EPacket.Client_RequestWorldData));
                expectedReceiveOrderOnServer.Add(
                    (EConnectionState.ServerSendingWorldData, EPacket.Client_Joined));
            }
            else
            {
                expectedReceiveOrderOnServer.Add(
                    (EConnectionState.ServerJoining, EPacket.Client_Joined));
            }

            int iPacketsReceived = 0;
            int iKeepAlivesReceived = 0;

            // Setup server hooks.
            void OnClientDispatch(EConnectionState eState, Packet packet)
            {
                if (packet.Type == EPacket.KeepAlive)
                {
                    ++iKeepAlivesReceived;
                }
                else
                {
                    Assert.Equal(expectedReceiveOrderOnServer[iPacketsReceived].Item1, eState);
                    Assert.Equal(expectedReceiveOrderOnServer[iPacketsReceived].Item2, packet.Type);
                    ++iPacketsReceived;
                }
            }

            ;
            ConnectionBase connServerSide = null;
            m_Server.Setup(s => s.Connected(It.IsAny<ConnectionServer>()))
                    .Callback<ConnectionBase>(
                        con =>
                        {
                            connServerSide = con;
                            con.Dispatcher.OnDispatch += (obj, args) =>
                                OnClientDispatch(args.State, args.Packet);
                        })
                    .CallBase();

            // Setup client
            Client client = new Client(m_WorldData.Object);
            client.Manager.Connect(
                m_Server.Object.ActiveConfig.LanAddress,
                m_Server.Object.ActiveConfig.LanPort);

            // Wait until the client is connected
            TestUtils.UpdateUntil(
                () => connServerSide != null &&
                      client.Session.Connection != null &&
                      (client.Session.Connection.State == EConnectionState.ClientPlaying ||
                       client.Session.Connection.State == EConnectionState.Disconnected),
                new List<IUpdateable>
                {
                    client.Manager,
                    m_NetManagerServer
                });
            Assert.NotNull(connServerSide);
            Assert.Equal(EConnectionState.ClientPlaying, client.Session.Connection.State);

            // Wait until the server received the client joined packet
            TestUtils.UpdateUntil(
                () => connServerSide.State == EConnectionState.ServerPlaying ||
                      connServerSide.State == EConnectionState.Disconnected,
                new List<IUpdateable>
                {
                    client.Manager,
                    m_NetManagerServer
                });
            Assert.Equal(EConnectionState.ServerPlaying, connServerSide.State);
            Assert.Equal(expectedReceiveOrderOnServer.Count, iPacketsReceived);
            m_WorldData.Verify(
                w => w.Receive(It.IsAny<ArraySegment<byte>>()),
                bExchangeWorldData ? Times.Once() : Times.Never());
            m_WorldData.Verify(
                w => w.SerializeInitialWorldState(),
                bExchangeWorldData ? Times.Once() : Times.Never());

            // Check if keep alive gets sent
            if (iKeepAlivesReceived == 0)
            {
                TestUtils.UpdateUntil(
                    () => iKeepAlivesReceived > 0,
                    new List<IUpdateable>
                    {
                        client.Manager
                    });
                Assert.True(iKeepAlivesReceived > 0);
            }

            // SendSync a sync package from client to server
            expectedReceiveOrderOnServer.Add((EConnectionState.ServerPlaying, EPacket.Sync));
            client.Session.Connection.Send(new Packet(EPacket.Sync, new byte[] { }));
            TestUtils.UpdateUntil(
                () => iPacketsReceived == expectedReceiveOrderOnServer.Count,
                new List<IUpdateable>
                {
                    client.Manager,
                    m_NetManagerServer
                });
            Assert.Equal(expectedReceiveOrderOnServer.Count, iPacketsReceived);
            m_WorldData.Verify(
                w => w.Receive(It.IsAny<ArraySegment<byte>>()),
                Times.Exactly(bExchangeWorldData ? 2 : 1));
        }

        private class Client
        {
            public readonly LiteNetManagerClient Manager;
            public readonly GameSession Session;

            public Client(ISaveData worldData)
            {
                Session = new GameSession(worldData);
                Manager = new LiteNetManagerClient(Session);
            }
        }
    }
}
