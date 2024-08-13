using Common.PacketHandlers;
using LiteNetLib;
using Moq;
using System.Collections.Generic;
using System.Reflection;
using ProtoBuf.WellKnownTypes;
using Xunit;

namespace Coop.Tests.Communication
{
    public class PacketManagerTest
    {
        [Fact]
        public void RegisterOnePacketHandler()
        {
            var packetManager = new PacketManager();
            var packetHandler = new TestPacketHandler();

            packetManager.RegisterPacketHandler(packetHandler);

            var registeredHandlers = typeof(PacketManager).GetField("packetHandlers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(packetManager) as Dictionary<PacketType, List<IPacketHandler>>;

            Assert.NotEmpty(registeredHandlers);
            Assert.True(registeredHandlers.ContainsKey(packetHandler.PacketType));

            Assert.Single(registeredHandlers);
            Assert.Single(registeredHandlers[packetHandler.PacketType]);
        }

        [Fact]
        public void RemoveOnePacketHandler()
        {
            var packetManager = new PacketManager();
            var packetHandler = new TestPacketHandler();
            var packetHandler2 = new TestPacketHandler();

            packetManager.RegisterPacketHandler(packetHandler);
            packetManager.RegisterPacketHandler(packetHandler2);

            var registeredHandlers = typeof(PacketManager).GetField("packetHandlers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(packetManager) as Dictionary<PacketType, List<IPacketHandler>>;

            Assert.NotEmpty(registeredHandlers);
            Assert.True(registeredHandlers.ContainsKey(packetHandler.PacketType));
            Assert.Equal(2, registeredHandlers[packetHandler.PacketType].Count);

            packetManager.RemovePacketHandler(packetHandler);

            Assert.Single(registeredHandlers);
            Assert.Single(registeredHandlers[packetHandler.PacketType]);
        }

        [Fact]
        public void RemoveAllPacketHandler()
        {
            var packetManager = new PacketManager();
            var packetHandler = new TestPacketHandler();

            packetManager.RegisterPacketHandler(packetHandler);

            var registeredHandlers = typeof(PacketManager).GetField("packetHandlers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(packetManager) as Dictionary<PacketType, List<IPacketHandler>>;

            packetManager.RemovePacketHandler(packetHandler);

            Assert.Empty(registeredHandlers);
        }

        [Fact]
        public void HandlePacket()
        {
            var handleCounter = 0;
            var packetHandler = new Mock<IPacketHandler>();
            packetHandler.Setup(m => m.HandlePacket(It.IsAny<NetPeer>(), It.IsAny<IPacket>()))
                         .Callback(() => handleCounter++);
            packetHandler.Setup(m => m.PacketType).Returns(() => PacketType.Test);

            var packetManager = new PacketManager();



            packetManager.RegisterPacketHandler(packetHandler.Object);

            var registeredHandlers = typeof(PacketManager).GetField("packetHandlers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(packetManager) as Dictionary<PacketType, List<IPacketHandler>>;

            Assert.NotEmpty(registeredHandlers);
            Assert.True(registeredHandlers.ContainsKey(packetHandler.Object.PacketType));
            Assert.Single(registeredHandlers[packetHandler.Object.PacketType]);

            var packet = new TestPacket();

            Assert.Equal(0, handleCounter);

            packetManager.HandleReceive(null, packet);

            Assert.Equal(1, handleCounter);

            packetManager.HandleReceive(null, packet);

            Assert.Equal(2, handleCounter);
        }
    }

    class TestPacketHandler : IPacketHandler
    {
        public PacketType PacketType => PacketType.Test;

        public int HandleCount = 0;

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            HandleCount++;
        }

        public void Dispose()
        {
        }
    }

    class TestPacket : IPacket
    {
        public PacketType PacketType => PacketType.Test;

        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;
        public string SubKey => string.Empty;
    }
}