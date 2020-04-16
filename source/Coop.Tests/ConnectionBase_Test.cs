using Coop.Network;
using Moq;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Coop.Tests
{
    public class ConnectionBase_Test
    {
        private readonly Mock<INetworkConnection> m_NetworkConnection;
        private readonly ConnectionBase m_Connection;

        private const int FragmentLength = 100;
        public ConnectionBase_Test()
        {
            m_NetworkConnection = new Mock<INetworkConnection>();
            m_NetworkConnection.Setup(con => con.FragmentLength).Returns(FragmentLength);
            m_NetworkConnection.Setup(con => con.MaxPackageLength).Returns(100000);
            m_Connection = new Mock<ConnectionBase>(m_NetworkConnection.Object).Object;
        }

        [Fact]
        public void DelegatesToSendRaw()
        {
            // Setup
            Packet packet = new Packet(Protocol.EPacket.Client_Hello, new byte[100]);
            MemoryStream stream = new MemoryStream();
            new PacketWriter(packet).Write(new BinaryWriter(stream));

            // Send
            m_Connection.Send(packet);

            // Verify
            m_NetworkConnection.Verify(con => con.SendRaw(It.Is<byte[]>(arg => arg.SequenceEqual(stream.ToArray()))));
        }
    }
}
