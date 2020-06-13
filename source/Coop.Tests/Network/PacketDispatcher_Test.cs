using System;
using Network;
using Network.Infrastructure;
using Network.Protocol;
using Xunit;

namespace Coop.Tests.Network
{
    public class PacketDispatcher_Test
    {
        public PacketDispatcher_Test()
        {
            m_Dispatcher = new PacketDispatcher();
            m_Packet = new Packet(EPacket.Client_Hello, new byte[0]);
        }

        private readonly PacketDispatcher m_Dispatcher;
        private readonly Packet m_Packet;

        private class EventHandlerStatic
        {
            [ThreadStatic] public static Action<Packet> s_OnStaticHandlerCalled;

            [PacketHandler(EConnectionState.Disconnected, EPacket.Client_Hello)]
            private static void StaticHandler(Packet packet)
            {
                s_OnStaticHandlerCalled(packet);
            }
        }

        private class EventHandlerNonStatic
        {
            public Action<Packet> OnHandlerCalled;

            [PacketHandler(EConnectionState.Disconnected, EPacket.Client_Hello)]
            private void NonStaticHandler(Packet packet)
            {
                OnHandlerCalled(packet);
            }
        }

        [Fact]
        public void DuplicateNonStaticRegistrationFails()
        {
            EventHandlerNonStatic eventHandler = new EventHandlerNonStatic();
            m_Dispatcher.RegisterPacketHandlers(eventHandler);
            Assert.Throws<DuplicatePacketHandlerRegistration>(
                () => m_Dispatcher.RegisterPacketHandlers(eventHandler));
        }

        [Fact]
        public void DuplicateStaticRegistrationFails()
        {
            m_Dispatcher.RegisterPacketHandlers(typeof(EventHandlerStatic));
            Assert.Throws<DuplicatePacketHandlerRegistration>(
                () => m_Dispatcher.RegisterPacketHandlers(typeof(EventHandlerStatic)));
        }

        [Fact]
        public void NonStaticHandlerRegister()
        {
            bool wasCalled = false;
            EventHandlerNonStatic eventHandler = new EventHandlerNonStatic();
            eventHandler.OnHandlerCalled = packet =>
            {
                wasCalled = true;
                Assert.Equal(m_Packet, packet);
            };
            m_Dispatcher.RegisterPacketHandlers(eventHandler);
            m_Dispatcher.Dispatch(EConnectionState.Disconnected, m_Packet);
            Assert.True(wasCalled);
        }

        [Fact]
        public void RegisterStaticAndNonStatic()
        {
            // Static
            bool wasStaticCalled = false;
            EventHandlerStatic.s_OnStaticHandlerCalled = packet =>
            {
                wasStaticCalled = true;
                Assert.Equal(m_Packet, packet);
            };
            m_Dispatcher.RegisterPacketHandlers(typeof(EventHandlerStatic));

            // Non-static
            bool wasNonStaticCalled = false;
            EventHandlerNonStatic eventHandler = new EventHandlerNonStatic();
            eventHandler.OnHandlerCalled = packet =>
            {
                wasNonStaticCalled = true;
                Assert.Equal(m_Packet, packet);
            };
            m_Dispatcher.RegisterPacketHandlers(eventHandler);

            m_Dispatcher.Dispatch(EConnectionState.Disconnected, m_Packet);
            Assert.True(wasStaticCalled);
            Assert.True(wasNonStaticCalled);
        }

        [Fact]
        public void StaticHandlerRegistered()
        {
            bool wasCalled = false;
            EventHandlerStatic.s_OnStaticHandlerCalled = packet =>
            {
                wasCalled = true;
                Assert.Equal(m_Packet, packet);
            };
            m_Dispatcher.RegisterPacketHandlers(typeof(EventHandlerStatic));
            m_Dispatcher.Dispatch(EConnectionState.Disconnected, m_Packet);
            Assert.True(wasCalled);
        }

        [Fact]
        public void UnregisterWorks()
        {
            // Register
            bool wasCalled = false;
            EventHandlerNonStatic eventHandler = new EventHandlerNonStatic();
            eventHandler.OnHandlerCalled = packet =>
            {
                wasCalled = true;
                Assert.Equal(m_Packet, packet);
            };
            m_Dispatcher.RegisterPacketHandlers(eventHandler);
            m_Dispatcher.Dispatch(EConnectionState.Disconnected, m_Packet);
            Assert.True(wasCalled);

            // Unregister
            wasCalled = false;
            m_Dispatcher.UnregisterPacketHandlers(eventHandler);
            m_Dispatcher.Dispatch(EConnectionState.Disconnected, m_Packet);
            Assert.False(wasCalled);
        }
    }
}
