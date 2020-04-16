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
        public ClientServerCommunication_Test()
        {
            m_Server = new Mock<Server> { CallBase = true };
            m_Server.Object.Start(TestUtils.GetTestingConfig());
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
            List<(EConnectionState, Protocol.EPacket)> expectedOrder = new List<(EConnectionState, Protocol.EPacket)>();
            expectedOrder.Add((EConnectionState.ServerAwaitingClient, Protocol.EPacket.Client_Hello));
            expectedOrder.Add((EConnectionState.ServerAwaitingClient, Protocol.EPacket.Client_Info));
            int iPacketsReceived = 0;

            // Setup server hooks.
            void OnClientDispatch(EConnectionState eState, Packet packet)
            {
                Assert.Equal(expectedOrder[iPacketsReceived].Item1, eState);
                Assert.Equal(expectedOrder[iPacketsReceived].Item2, packet.Type);
                ++iPacketsReceived;
            };
            m_Server.Setup(s => s.OnConnected(It.IsAny<ConnectionBase>())).Callback<ConnectionBase>((con) => con.Dispatcher.OnDispatch += (obj, args) => OnClientDispatch(args.State, args.Packet));

            // Setup client
            Client client = new Client();
            client.Manager.Connect(m_Server.Object.ActiveConfig.lanAddress.ToString(), m_Server.Object.ActiveConfig.lanPort);

            // Wait until handshake is complete
            TestUtils.UpdateUntil(() => client.Session.Connection != null && client.Session.Connection.State == EConnectionState.ClientJoining, new List<IUpdateable>() { client.Manager });
            Assert.Equal(expectedOrder.Count, iPacketsReceived);
        }
    }
}
