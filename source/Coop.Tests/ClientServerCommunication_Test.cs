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
        public ClientServerCommunication_Test()
        {
            ServerConfiguration config = TestUtils.GetTestingConfig();
            config.keepAliveInterval = m_keepAliveInterval;
            m_Server = new Mock<Server> { CallBase = true };
            m_Server.Object.Start(config);
            m_NetManagerServer = new NetManagerServer(m_Server.Object);
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
        void ClientSendOrder()
        {
            List<(EConnectionState, Protocol.EPacket)> expectedReceiveOrderOnServer = new List<(EConnectionState, Protocol.EPacket)>();
            expectedReceiveOrderOnServer.Add((EConnectionState.ServerAwaitingClient, Protocol.EPacket.Client_Hello));
            expectedReceiveOrderOnServer.Add((EConnectionState.ServerAwaitingClient, Protocol.EPacket.Client_Info));
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
            Client client = new Client();
            client.Manager.Connect(m_Server.Object.ActiveConfig.lanAddress.ToString(), m_Server.Object.ActiveConfig.lanPort);

            // Wait until handshake is complete
            TestUtils.UpdateUntil(() => client.Session.Connection != null && client.Session.Connection.State == EConnectionState.ClientJoining, new List<IUpdateable>() { client.Manager });
            Assert.Equal(expectedReceiveOrderOnServer.Count, iPacketsReceived);
            Assert.NotNull(connServerSide);
            Assert.Equal(EConnectionState.ClientJoining, client.Session.Connection.State);
            Assert.Equal(EConnectionState.ServerJoining, connServerSide.State);

            if (iKeepAlivesReceived == 0)
            {
                TestUtils.UpdateUntil(() => iKeepAlivesReceived > 0, new List<IUpdateable>() { client.Manager });
                Assert.True(iKeepAlivesReceived > 0);
            }
        }
    }
}
