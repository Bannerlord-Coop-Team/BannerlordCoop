using System;
using System.Collections.Generic;
using Coop.Multiplayer;
using Coop.Network;
using Moq;
using Xunit;

namespace Coop.Tests
{
    public class ConnectionServer_Test
    {
        private readonly Mock<INetworkConnection> m_NetworkConnection = TestUtils.CreateMockConnection();
        private readonly Mock<IGameStatePersistence> m_GamePersistence;
        private readonly Mock<ISaveData> m_WorldData = TestUtils.CreateMockSaveData();
        private readonly ConnectionServer m_Connection;
        private ArraySegment<byte> m_PersistenceReceiveParam;

        private List<ArraySegment<byte>> m_SendRawParams = new List<ArraySegment<byte>>();
        public ConnectionServer_Test()
        {
            m_NetworkConnection.Setup(con => con.SendRaw(It.IsAny<ArraySegment<byte>>(), It.IsAny<EDeliveryMethod>())).Callback((ArraySegment<byte> arg, EDeliveryMethod eMethod) => m_SendRawParams.Add(arg));
            m_GamePersistence = new Mock<IGameStatePersistence>();
            m_GamePersistence.Setup(per => per.Receive(It.IsAny<ArraySegment<byte>>())).Callback((ArraySegment<byte> arg) => m_PersistenceReceiveParam = arg);
            m_Connection = new ConnectionServer(m_NetworkConnection.Object, m_GamePersistence.Object, m_WorldData.Object);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]

        void VerifyStateTransitionsUntilConnected(bool bWithWorldDataExchange)
        {
            // Init
            Assert.Equal(EConnectionState.Disconnected, m_Connection.State);
            m_Connection.PrepareForClientConnection();
            Assert.Equal(EConnectionState.ServerAwaitingClient, m_Connection.State);

            // Send client hello
            var clientHello = TestUtils.MakeRaw(
                Protocol.EPacket.Client_Hello,
                new Protocol.Client_Hello(Protocol.Version).Serialize());
            m_Connection.Receive(clientHello);

            // Expect server request client info
            var response = TestUtils.MakeRaw(
                Protocol.EPacket.Server_RequestClientInfo,
                new Protocol.Server_RequestClientInfo().Serialize());
            Assert.Equal(response, m_SendRawParams[^1]);
            Assert.Equal(EConnectionState.ServerAwaitingClient, m_Connection.State);

            // Respond with client info
            var clientInfo = TestUtils.MakeRaw(
                Protocol.EPacket.Client_Info,
                new Protocol.Client_Info(new Player("Unknown")).Serialize());
            m_Connection.Receive(clientInfo);

            var joinRequestAccepted = TestUtils.MakeRaw(
                Protocol.EPacket.Server_JoinRequestAccepted,
                new Protocol.Server_JoinRequestAccepted().Serialize());
            Assert.Equal(joinRequestAccepted, m_SendRawParams[^1]);
            Assert.Equal(EConnectionState.ServerJoining, m_Connection.State);

            if (bWithWorldDataExchange)
            {
                // Request world data
                var worldDataRequest = TestUtils.MakeRaw(
                    Protocol.EPacket.Client_RequestWorldData,
                    new Protocol.Client_RequestWorldData().Serialize());
                m_Connection.Receive(worldDataRequest);

                var worldData = TestUtils.MakeRaw(
                    Protocol.EPacket.Server_WorldData,
                    m_WorldData.Object.SerializeInitialWorldState());
                Assert.Equal(worldData, m_SendRawParams[^1]);
                Assert.Equal(EConnectionState.ServerSendingWorldData, m_Connection.State);
            }

            // client joined
            var joined = TestUtils.MakeRaw(
                Protocol.EPacket.Client_Joined,
                new Protocol.Client_Joined().Serialize());
            m_Connection.Receive(joined);
            Assert.Equal(EConnectionState.ServerConnected, m_Connection.State);
        }
    }
}
