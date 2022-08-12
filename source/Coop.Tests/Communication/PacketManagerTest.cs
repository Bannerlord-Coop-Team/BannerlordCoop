using Autofac;
using Coop.Communication.PacketHandlers;
using Xunit;

namespace Coop.Tests.Communication
{
    public class PacketManagerTest
    {
        [Fact]
        public void RegisterOnePacketHandler()
        {
            var container = Bootstrap.InitializeAsServer();
            //using var packetManager = container.Resolve<IPacketManager>();

            Assert.Fail("To implement.");
        }

        [Fact]
        public void RemoveOnePacketHandler()
        {
            Assert.Fail("To implement.");
        }

        [Fact]
        public void HandleOnePacket()
        {
            Assert.Fail("To implement.");
        }

        [Fact]
        public void SendOnePacket()
        {
            Assert.Fail("To implement.");
        }

        [Fact]
        public void SendOnePacketExcept()
        {
            Assert.Fail("To implement.");
        }
    }
}