using System;
using System.IO;
using System.Linq;
using Coop.Multiplayer;
using Coop.Network;
using Moq;
using Xunit;

namespace Coop.Tests
{
    public class ConnectionClient_Test
    {
        private readonly Mock<INetworkConnection> m_NetworkConnection = TestUtils.CreateMockConnection();
        private readonly Mock<IGameStatePersistence> m_GamePersistence;
        private readonly Mock<ISaveData> m_WorldData = TestUtils.CreateMockSaveData();
        private readonly ConnectionClient m_Connection;
        private ArraySegment<byte> m_PersistenceReceiveParam;

        private ArraySegment<byte> m_SendRawParam;
        public ConnectionClient_Test()
        {
            m_NetworkConnection.Setup(con => con.SendRaw(It.IsAny<ArraySegment<byte>>(), It.IsAny<EDeliveryMethod>())).Callback((ArraySegment<byte> arg, EDeliveryMethod eMethod) => m_SendRawParam = arg);
            m_GamePersistence = new Mock<IGameStatePersistence>();
            m_GamePersistence.Setup(per => per.Receive(It.IsAny<ArraySegment<byte>>())).Callback((ArraySegment<byte> arg) => m_PersistenceReceiveParam = arg);
            m_Connection = new ConnectionClient(m_NetworkConnection.Object, m_GamePersistence.Object, m_WorldData.Object);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        void VerifyStateTransitionsUntilConnected(bool bExchangeWorldData)
        {
            m_WorldData.Setup(d => d.RequiresInitialWorldData).Returns(bExchangeWorldData);

            // Init
            Assert.Equal(EConnectionState.Disconnected, m_Connection.State);
            m_Connection.Connect();

            // Expect client hello
            m_NetworkConnection.Verify(c => c.SendRaw(It.IsAny<ArraySegment<byte>>(), It.IsAny<EDeliveryMethod>()), Times.Once);
            var expectedSentData = TestUtils.MakeRaw(
                Protocol.EPacket.Client_Hello,
                new Protocol.Client_Hello(Protocol.Version).Serialize());
            Assert.Equal(expectedSentData, m_SendRawParam);
            Assert.Equal(EConnectionState.ClientJoinRequesting, m_Connection.State);


            // Ack client hello
            var response = TestUtils.MakeRaw(
                Protocol.EPacket.Server_RequestClientInfo,
                new Protocol.Server_RequestClientInfo().Serialize());
            m_Connection.Receive(response);
            Assert.Equal(EConnectionState.ClientJoinRequesting, m_Connection.State);

            // Expect client info
            expectedSentData = TestUtils.MakeRaw(
                Protocol.EPacket.Client_Info,
                new Protocol.Client_Info(new Player("Unknown")).Serialize());
            Assert.Equal(expectedSentData, m_SendRawParam);
            Assert.Equal(EConnectionState.ClientJoinRequesting, m_Connection.State);

            // Ack client info
            response = TestUtils.MakeRaw(
                Protocol.EPacket.Server_JoinRequestAccepted,
                new Protocol.Server_JoinRequestAccepted().Serialize());
            m_Connection.Receive(response);

            if (bExchangeWorldData)
            {
                expectedSentData = TestUtils.MakeRaw(
                    Protocol.EPacket.Client_RequestWorldData,
                    new Protocol.Client_RequestWorldData().Serialize());
                Assert.Equal(expectedSentData, m_SendRawParam);
                Assert.Equal(EConnectionState.ClientAwaitingWorldData, m_Connection.State);

                // Send world data to client
                response = TestUtils.MakeRaw(
                    Protocol.EPacket.Server_WorldData,
                    m_WorldData.Object.SerializeInitialWorldState());
                m_Connection.Receive(response);
                Assert.Equal(EConnectionState.ClientConnected, m_Connection.State);
            }

            // Expect client joined
            expectedSentData = TestUtils.MakeRaw(
                Protocol.EPacket.Client_Joined,
                new Protocol.Client_Joined().Serialize());
            Assert.Equal(expectedSentData, m_SendRawParam);
            Assert.Equal(EConnectionState.ClientConnected, m_Connection.State);

            // Send keep alive
            var keepAliveFromServer = TestUtils.MakeKeepAlive(42);
            m_Connection.Receive(keepAliveFromServer);
            Assert.Equal(EConnectionState.ClientConnected, m_Connection.State);

            // Expect client keep alive response
            expectedSentData = keepAliveFromServer;
            Assert.Equal(expectedSentData, m_SendRawParam);
        }

        [Fact]
        void ReceiveForPersistenceIsRelayed()
        {
            // Bring connection to EConnectionState.ClientConnected
            VerifyStateTransitionsUntilConnected(false);
            Assert.Equal(EConnectionState.ClientConnected, m_Connection.State);

            // Persistence has not received anything yet
            Assert.Null(m_PersistenceReceiveParam.Array);

            // Generate a payload
            var persistencePayload = TestUtils.MakePersistencePayload(50);

            // Receive
            m_Connection.Receive(persistencePayload);

            // Verify
            Assert.Equal(persistencePayload, m_PersistenceReceiveParam);
            Assert.Equal(EConnectionState.ClientConnected, m_Connection.State);

            // Interweave a keep alive
            var keepAliveFromServer = TestUtils.MakeKeepAlive(42);
            m_Connection.Receive(keepAliveFromServer);
            Assert.Equal(keepAliveFromServer, m_SendRawParam); // Client ack

            // Send another persistence packet
            m_PersistenceReceiveParam = new ArraySegment<byte>();
            m_Connection.Receive(persistencePayload);

            // Verify
            Assert.Equal(persistencePayload, m_PersistenceReceiveParam);
            Assert.Equal(EConnectionState.ClientConnected, m_Connection.State);
        }
    }
}
