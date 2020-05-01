using Coop.Common;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using Coop.Network;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Coop.Tests
{
    public class ClientServerCommunication_Test
    {
        private readonly Mock<Server> m_Server;
        private readonly LiteNetManagerServer m_NetManagerServer;
        private readonly TimeSpan m_keepAliveInterval = TimeSpan.FromMilliseconds(50);
        private readonly Mock<ISaveData> m_WorldData = TestUtils.CreateMockSaveData();
        public ClientServerCommunication_Test()
        {
            ServerConfiguration config = TestUtils.GetTestingConfig();
            config.KeepAliveInterval = m_keepAliveInterval;
            m_Server = new Mock<Server> { CallBase = true };
            m_Server.Object.Start(config);
            m_NetManagerServer = new LiteNetManagerServer(m_Server.Object, m_WorldData.Object);
            m_NetManagerServer.StartListening();
        }

        private class Client
        {
            public readonly GameSession Session;
            public readonly LiteNetManagerClient Manager;

            public Client(ISaveData worldData)
            {
                Session = new GameSession(worldData);
                Manager = new LiteNetManagerClient(Session);
            }
        }

        [Fact]
        void ClientSendOrder()
        {
            List<(EConnectionState, Protocol.EPacket)> expectedReceiveOrderOnServer = new List<(EConnectionState, Protocol.EPacket)>();
            expectedReceiveOrderOnServer.Add((EConnectionState.ServerAwaitingClient, Protocol.EPacket.Client_Hello));
            expectedReceiveOrderOnServer.Add((EConnectionState.ServerAwaitingClient, Protocol.EPacket.Client_Info));
            expectedReceiveOrderOnServer.Add((EConnectionState.ServerSendingWorldData, Protocol.EPacket.Client_Joined));
            int iPacketsReceived = 0;
            int iKeepAlivesReceived = 0;

            // Setup server hooks.
            void OnClientDispatch(EConnectionState eState, Packet packet)
            {
                if (packet.Type == Protocol.EPacket.KeepAlive)
                {
                    ++iKeepAlivesReceived;
                }
                else
                {
                    Assert.Equal(expectedReceiveOrderOnServer[iPacketsReceived].Item1, eState);
                    Assert.Equal(expectedReceiveOrderOnServer[iPacketsReceived].Item2, packet.Type);
                    ++iPacketsReceived;
                }
            };
            ConnectionBase connServerSide = null;
            m_Server.Setup(s => s.Connected(It.IsAny<ConnectionServer>()))
                .Callback<ConnectionBase>((con) =>
                {
                    connServerSide = con;
                    con.Dispatcher.OnDispatch += (obj, args) => OnClientDispatch(args.State, args.Packet);
                })
                .CallBase();

            // Setup client
            Client client = new Client(m_WorldData.Object);
            client.Manager.Connect(m_Server.Object.ActiveConfig.LanAddress, m_Server.Object.ActiveConfig.LanPort);

            // Wait until the client is connected
            TestUtils.UpdateUntil(() => connServerSide != null && client.Session.Connection != null && (client.Session.Connection.State == EConnectionState.ClientConnected || client.Session.Connection.State == EConnectionState.Disconnected), new List<IUpdateable>() { client.Manager, m_NetManagerServer });
            Assert.NotNull(connServerSide);
            Assert.Equal(EConnectionState.ClientConnected, client.Session.Connection.State);

            // Wait until the server received the client joined packet
            TestUtils.UpdateUntil(() => connServerSide.State == EConnectionState.ServerConnected || connServerSide.State == EConnectionState.Disconnected, new List<IUpdateable>() { client.Manager, m_NetManagerServer });
            Assert.Equal(EConnectionState.ServerConnected, connServerSide.State);
            Assert.Equal(expectedReceiveOrderOnServer.Count, iPacketsReceived);
            m_WorldData.Verify(w => w.Receive(It.IsAny<ArraySegment<byte>>()), Times.Once());
            m_WorldData.Verify(w => w.SerializeInitialWorldState(), Times.Once());

            // Check if keep alive gets sent
            if (iKeepAlivesReceived == 0)
            {
                TestUtils.UpdateUntil(() => iKeepAlivesReceived > 0, new List<IUpdateable>() { client.Manager });
                Assert.True(iKeepAlivesReceived > 0);
            }

            // SendSync a sync package from client to server
            expectedReceiveOrderOnServer.Add((EConnectionState.ServerConnected, Protocol.EPacket.Sync));
            client.Session.Connection.Send(new Packet(Protocol.EPacket.Sync, new byte[] { }));
            TestUtils.UpdateUntil(() => iPacketsReceived == expectedReceiveOrderOnServer.Count, new List<IUpdateable>() { client.Manager, m_NetManagerServer });
            Assert.Equal(expectedReceiveOrderOnServer.Count, iPacketsReceived);
            m_WorldData.Verify(w => w.Receive(It.IsAny<ArraySegment<byte>>()), Times.Exactly(2));
        }
    }
}
