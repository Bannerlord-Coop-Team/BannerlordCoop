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
        private readonly NetManagerServer m_NetManagerServer;
        private TimeSpan m_keepAliveInterval = TimeSpan.FromMilliseconds(50);
        private readonly Mock<IWorldData> m_WorldData;
        public ClientServerCommunication_Test()
        {
            m_WorldData = new Mock<IWorldData>();
            m_WorldData.Setup(w => w.Receive(It.IsAny<byte[]>()))
                .Returns(true);
            m_WorldData.Setup(w => w.SerializeWorldState())
                .Returns(new byte[0]);

            ServerConfiguration config = TestUtils.GetTestingConfig();
            config.keepAliveInterval = m_keepAliveInterval;
            m_Server = new Mock<Server> { CallBase = true };
            m_Server.Object.Start(config);
            m_NetManagerServer = new NetManagerServer(m_Server.Object, m_WorldData.Object);
            m_NetManagerServer.StartListening();
        }

        private class Client
        {
            public readonly GameSession Session;
            public readonly NetManagerClient Manager;

            public Client(IWorldData worldData)
            {
                Session = new GameSession(worldData);
                Manager = new NetManagerClient(Session);
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
                if(packet.Type == Protocol.EPacket.Client_KeepAlive)
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
            m_Server.Setup(s => s.OnConnected(It.IsAny<ConnectionBase>()))
                .Callback<ConnectionBase>((con) => 
                {
                    connServerSide = con;
                    con.Dispatcher.OnDispatch += (obj, args) => OnClientDispatch(args.State, args.Packet);
                })
                .CallBase();

            // Setup client
            Client client = new Client(m_WorldData.Object);
            client.Manager.Connect(m_Server.Object.ActiveConfig.lanAddress.ToString(), m_Server.Object.ActiveConfig.lanPort);

            // Wait until the client is connected
            TestUtils.UpdateUntil(() => connServerSide != null && client.Session.Connection != null && (client.Session.Connection.State == EConnectionState.ClientConnected || client.Session.Connection.State == EConnectionState.Disconnected), new List<IUpdateable>() { client.Manager, m_NetManagerServer });
            Assert.NotNull(connServerSide);
            Assert.Equal(EConnectionState.ClientConnected, client.Session.Connection.State);

            // Wait until the server received the client joined packet
            TestUtils.UpdateUntil(() => connServerSide.State == EConnectionState.ServerConnected || connServerSide.State == EConnectionState.Disconnected, new List<IUpdateable>() { client.Manager, m_NetManagerServer });
            Assert.Equal(EConnectionState.ServerConnected, connServerSide.State);
            Assert.Equal(expectedReceiveOrderOnServer.Count, iPacketsReceived);
            m_WorldData.Verify(w => w.Receive(It.IsAny<byte[]>()), Times.Once());
            m_WorldData.Verify(w => w.SerializeWorldState(), Times.Once());

            // Check if keep alive gets sent
            if (iKeepAlivesReceived == 0)
            {
                TestUtils.UpdateUntil(() => iKeepAlivesReceived > 0, new List<IUpdateable>() { client.Manager });
                Assert.True(iKeepAlivesReceived > 0);
            }
        }
    }
}
