using Coop.Network;
using System;
using Xunit;

namespace Coop.Tests
{
    public class PacketDispatcher_Test
    {
        private readonly PacketDispatcher m_Dispatcher;
        private readonly Packet m_Packet;

        public PacketDispatcher_Test()
        {
            m_Dispatcher = new PacketDispatcher();
            m_Packet = new Packet(Protocol.EPacket.Client_Hello, new byte[0]);
        }

        private class EventHandlerStatic
        {
            [ThreadStatic] public static Action<Packet> s_OnStaticHandlerCalled;

            [PacketHandler(EConnectionState.Disconnected, Protocol.EPacket.Client_Hello)]
            static void StaticHandler(Packet packet)
            {
                s_OnStaticHandlerCalled(packet);
            }
        }

        [Fact]
        public void StaticHandlerRegistered()
        {
            bool wasCalled = false;
            EventHandlerStatic.s_OnStaticHandlerCalled = (packet) => 
            {
                wasCalled = true;
                Assert.Equal(m_Packet, packet);
            };
            m_Dispatcher.RegisterPacketHandlers(typeof(EventHandlerStatic));
            m_Dispatcher.Dispatch(EConnectionState.Disconnected, m_Packet);
            Assert.True(wasCalled);
        }

        private class EventHandlerNonStatic
        {
            public Action<Packet> OnHandlerCalled;
            [PacketHandler(EConnectionState.Disconnected, Protocol.EPacket.Client_Hello)]
            void NonStaticHandler(Packet packet)
            {
                OnHandlerCalled(packet);
            }
        }

        [Fact]
        public void NonStaticHandlerRegister()
        {
            bool wasCalled = false;
            var eventHandler = new EventHandlerNonStatic();
            eventHandler.OnHandlerCalled = (packet) =>
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
            EventHandlerStatic.s_OnStaticHandlerCalled = (packet) =>
            {
                wasStaticCalled = true;
                Assert.Equal(m_Packet, packet);
            };
            m_Dispatcher.RegisterPacketHandlers(typeof(EventHandlerStatic));

            // Non-static
            bool wasNonStaticCalled = false;
            var eventHandler = new EventHandlerNonStatic();
            eventHandler.OnHandlerCalled = (packet) =>
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
        public void DuplicateStaticRegistrationFails()
        {
            m_Dispatcher.RegisterPacketHandlers(typeof(EventHandlerStatic));
            Assert.Throws<DuplicatePacketHandlerRegistration>(() => m_Dispatcher.RegisterPacketHandlers(typeof(EventHandlerStatic)));
        }
        [Fact]
        public void DuplicateNonStaticRegistrationFails()
        {
            var eventHandler = new EventHandlerNonStatic();
            m_Dispatcher.RegisterPacketHandlers(eventHandler);
            Assert.Throws<DuplicatePacketHandlerRegistration>(() => m_Dispatcher.RegisterPacketHandlers(eventHandler));
        }
        [Fact]
        public void UnregisterWorks()
        {
            // Register
            bool wasCalled = false;
            var eventHandler = new EventHandlerNonStatic();
            eventHandler.OnHandlerCalled = (packet) =>
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
