using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Network.Infrastructure;
using Network.Protocol;
using Xunit;
using Version = Network.Protocol.Version;

namespace Coop.Tests.Network
{
    public class ConnectionServer_Test
    {
        private readonly ConnectionServer m_Connection;
        private readonly Mock<IGameStatePersistence> m_GamePersistence;

        private readonly Mock<INetworkConnection> m_NetworkConnection =
            TestUtils.CreateMockConnection();

        private readonly List<ArraySegment<byte>> m_SendRawParams = new List<ArraySegment<byte>>();

        private ArraySegment<byte> m_PersistenceReceiveParam;

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
                m_GamePersistence.Object);
            CompatibilityInfo.ModuleProvider = new ModuleInfoProviderMock();
        }

        [Fact]
        private void VerifyStateTransitionsUntilConnected()
        {
            // Init
            Assert.Equal(EServerConnectionState.AwaitingClient, m_Connection.State);

            // Send client hello
            ArraySegment<byte> clientHello = TestUtils.MakeRaw(
                EPacket.Client_Hello,
                new Client_Hello(Version.Number, CompatibilityInfo.Get()).Serialize());
            m_Connection.Receive(clientHello);

            // Expect server request client info
            ArraySegment<byte> response = TestUtils.MakeRaw(
                EPacket.Server_RequestClientInfo,
                new Server_RequestClientInfo().Serialize());
            Assert.Equal(response, m_SendRawParams[m_SendRawParams.Count - 1]);
            Assert.Equal(EServerConnectionState.AwaitingClient, m_Connection.State);

            // Respond with client info
            ArraySegment<byte> clientInfo = TestUtils.MakeRaw(
                EPacket.Client_Info,
                new Client_Info(new Player("Unknown")).Serialize());
            m_Connection.Receive(clientInfo);
            Assert.Equal(EServerConnectionState.ClientJoining, m_Connection.State);

            ArraySegment<byte> joinRequestAccepted = TestUtils.MakeRaw(
                EPacket.Server_JoinRequestAccepted,
                new Server_JoinRequestAccepted().Serialize());
            Assert.Equal(joinRequestAccepted, m_SendRawParams[m_SendRawParams.Count - 1]);

            ArraySegment<byte> clientJoined = TestUtils.MakeRaw(
                EPacket.Client_Loaded,
                new Client_Joined().Serialize());
            m_Connection.Receive(clientJoined);
            Assert.Equal(EServerConnectionState.Ready, m_Connection.State);
        }
    }
}
