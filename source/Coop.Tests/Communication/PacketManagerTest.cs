using Autofac;
using Common.Messaging;
using Coop.Core.Communication.PacketHandlers;
using Coop.Core.Server;
using LiteNetLib;
using System.Collections.Generic;
using System.Reflection;
using System;
using Xunit;

namespace Coop.Tests.Communication
{
    public class PacketManagerTest
    {
        [Fact]
        public void RegisterOnePacketHandler()
        {
            var container = Bootstrap.InitializeAsServer();
            var packetManager = container.Resolve<ICoopServer>().PacketManager;

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
            var container = Bootstrap.InitializeAsServer();
            var packetManager = container.Resolve<ICoopServer>().PacketManager;

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
            var container = Bootstrap.InitializeAsServer();
            var packetManager = container.Resolve<ICoopServer>().PacketManager;

            var packetHandler = new TestPacketHandler();

            packetManager.RegisterPacketHandler(packetHandler);

            var registeredHandlers = typeof(PacketManager).GetField("packetHandlers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(packetManager) as Dictionary<PacketType, List<IPacketHandler>>;

            packetManager.RemovePacketHandler(packetHandler);

            Assert.Empty(registeredHandlers);
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

    class TestPacketHandler : IPacketHandler
    {
        public PacketType PacketType => PacketType.Test;

        public int HandleCount = 0;

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            HandleCount++;
        }
    }

    class TestPacket : IPacket
    {
        public PacketType PacketType => PacketType.Test;

        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;
    }
}