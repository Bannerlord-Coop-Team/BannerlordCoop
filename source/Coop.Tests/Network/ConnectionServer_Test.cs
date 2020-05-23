using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Moq;
using Network.Infrastructure;
using Network.Protocol;
using Xunit;
using Version = System.Version;

namespace Coop.Tests
{
    public class ConnectionServer_Test
    {
        private readonly ConnectionServer m_Connection;
        private readonly Mock<IGameStatePersistence> m_GamePersistence;

        private readonly Mock<INetworkConnection> m_NetworkConnection =
            TestUtils.CreateMockConnection();

        private readonly Mock<ISaveData> m_WorldData = TestUtils.CreateMockSaveData();
        private ArraySegment<byte> m_PersistenceReceiveParam;

        private readonly List<ArraySegment<byte>> m_SendRawParams = new List<ArraySegment<byte>>();

        public ConnectionServer_Test()
        {
            m_NetworkConnection
                .Setup(
                    con => con.SendRaw(It.IsAny<ArraySegment<byte>>(), It.IsAny<EDeliveryMethod>()))
                .Callback(
                    (ArraySegment<byte> arg, EDeliveryMethod eMethod) => m_SendRawParams.Add(arg));
            m_GamePersistence = new Mock<IGameStatePersistence>();
            m_GamePersistence.Setup(per => per.Receive(It.IsAny<ArraySegment<byte>>()))
                             .Callback(
                                 (ArraySegment<byte> arg) => m_PersistenceReceiveParam = arg);
            m_Connection = new ConnectionServer(
                m_NetworkConnection.Object,
                m_GamePersistence.Object,
                m_WorldData.Object);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        private void VerifyStateTransitionsUntilConnected(bool bWithWorldDataExchange)
        {
            // Init
            Assert.Equal(EConnectionState.Disconnected, m_Connection.State);
            m_Connection.PrepareForClientConnection();
            Assert.Equal(EConnectionState.ServerAwaitingClient, m_Connection.State);

            // Send client hello
            ArraySegment<byte> clientHello = TestUtils.MakeRaw(
                EPacket.Client_Hello,
                new Client_Hello(Network.Protocol.Version.Number).Serialize());
            m_Connection.Receive(clientHello);

            // Expect server request client info
            ArraySegment<byte> response = TestUtils.MakeRaw(
                EPacket.Server_RequestClientInfo,
                new Server_RequestClientInfo().Serialize());
            Assert.Equal(response, m_SendRawParams[^1]);
            Assert.Equal(EConnectionState.ServerAwaitingClient, m_Connection.State);

            // Respond with client info
            ArraySegment<byte> clientInfo = TestUtils.MakeRaw(
                EPacket.Client_Info,
                new Client_Info(new Player("Unknown")).Serialize());
            m_Connection.Receive(clientInfo);

            ArraySegment<byte> joinRequestAccepted = TestUtils.MakeRaw(
                EPacket.Server_JoinRequestAccepted,
                new Server_JoinRequestAccepted().Serialize());
            Assert.Equal(joinRequestAccepted, m_SendRawParams[^1]);
            Assert.Equal(EConnectionState.ServerJoining, m_Connection.State);

            if (bWithWorldDataExchange)
            {
                // Request world data
                ArraySegment<byte> worldDataRequest = TestUtils.MakeRaw(
                    EPacket.Client_RequestWorldData,
                    new Client_RequestWorldData().Serialize());
                m_Connection.Receive(worldDataRequest);

                ArraySegment<byte> worldData = TestUtils.MakeRaw(
                    EPacket.Server_WorldData,
                    m_WorldData.Object.SerializeInitialWorldState());
                Assert.Equal(worldData, m_SendRawParams[^1]);
                Assert.Equal(EConnectionState.ServerSendingWorldData, m_Connection.State);
            }

            // client joined
            ArraySegment<byte> joined = TestUtils.MakeRaw(
                EPacket.Client_Joined,
                new Client_Joined().Serialize());
            m_Connection.Receive(joined);
            Assert.Equal(EConnectionState.ServerPlaying, m_Connection.State);
        }
    }
}
