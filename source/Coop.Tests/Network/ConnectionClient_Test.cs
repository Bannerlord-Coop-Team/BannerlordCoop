using Common;
using System;
using Moq;
using Network.Infrastructure;
using Network.Protocol;
using Coop;
using Xunit;
using Version = Network.Protocol.Version;

namespace Coop.Tests.Network
{
    public class ConnectionClient_Test
    {
        public ConnectionClient_Test()
        {
            m_NetworkConnection
                .Setup(
                    con => con.SendRaw(It.IsAny<ArraySegment<byte>>(), It.IsAny<EDeliveryMethod>()))
                .Callback(
                    (ArraySegment<byte> arg, EDeliveryMethod eMethod) => m_SendRawParam = arg);
            m_GamePersistence = new Mock<IGameStatePersistence>();
            m_GamePersistence.Setup(per => per.Receive(It.IsAny<ArraySegment<byte>>()))
                             .Callback(
                                 (ArraySegment<byte> arg) => m_PersistenceReceiveParam = arg);
            m_Connection = new ConnectionClient(
                m_NetworkConnection.Object,
                m_GamePersistence.Object);
            CompatibilityInfo.ModuleProvider = new ModuleInfoProviderMock();
        }

        private readonly Mock<INetworkConnection> m_NetworkConnection =
            TestUtils.CreateMockConnection();

        private readonly Mock<IGameStatePersistence> m_GamePersistence;
        private readonly Mock<ISaveData> m_WorldData = TestUtils.CreateMockSaveData();
        private readonly ConnectionClient m_Connection;
        private ArraySegment<byte> m_PersistenceReceiveParam;

        private ArraySegment<byte> m_SendRawParam;

        [Fact]
        private void VerifyStateTransitionsUntilConnected()
        {
            //m_WorldData.Setup(d => d.RequiresInitialWorldData).Returns(bExchangeWorldData);

            // Init
            Assert.Equal(EClientConnectionState.Disconnected, m_Connection.State);
            m_Connection.Connect();

            // Expect client hello
            m_NetworkConnection.Verify(
                c => c.SendRaw(It.IsAny<ArraySegment<byte>>(), It.IsAny<EDeliveryMethod>()),
                Times.Once);
            ArraySegment<byte> expectedSentData = TestUtils.MakeRaw(
                EPacket.Client_Hello,
                new Client_Hello(Version.Number, CompatibilityInfo.Get()).Serialize());
            Assert.Equal(expectedSentData, m_SendRawParam);

            // Ack client hello
            ArraySegment<byte> response = TestUtils.MakeRaw(
                EPacket.Server_RequestClientInfo,
                new Server_RequestClientInfo().Serialize());
            m_Connection.Receive(response);
            Assert.Equal(EClientConnectionState.JoinRequesting, m_Connection.State);

            // Expect client info
            expectedSentData = TestUtils.MakeRaw(
                EPacket.Client_Info,
                new Client_Info(new Player("Unknown")).Serialize());
            Assert.Equal(expectedSentData, m_SendRawParam);

            // Server accepts join request
            ArraySegment<byte> joinRequestAccepted = TestUtils.MakeRaw(
                EPacket.Server_JoinRequestAccepted,
                new Server_JoinRequestAccepted().Serialize());
            m_Connection.Receive(joinRequestAccepted);
            Assert.True(m_Connection.State.Equals(EClientConnectionState.Connected));
        }
    }
}
