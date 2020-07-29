using System;
using JetBrains.Annotations;
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

        private class EventHandlerNonStatic
        {
            public Action<Packet> OnHandlerCalled;

            public ConnectionServerSM stateMachine;

            public EventHandlerNonStatic()
            {
                stateMachine = new ConnectionServerSM();
            }

            [ConnectionServerPacketHandler(EServerConnectionState.AwaitingClient, EPacket.Client_Hello)]
            public void NonStaticHandler(ConnectionBase con, Packet packet)
            {
                OnHandlerCalled(packet);
            }
        }

        [Fact]
        public void DuplicateRegistrationFails()
        {
            EventHandlerNonStatic eventHandler = new EventHandlerNonStatic();
            m_Dispatcher.RegisterPacketHandler(eventHandler.NonStaticHandler);
            Assert.Throws<DuplicatePacketHandlerRegistration>(
                () => m_Dispatcher.RegisterPacketHandler(eventHandler.NonStaticHandler));
        }

        [Fact]
        public void HandlerRegister()
        {
            bool wasCalled = false;
            EventHandlerNonStatic eventHandler = new EventHandlerNonStatic();
            eventHandler.OnHandlerCalled = packet =>
            {
                wasCalled = true;
                Assert.Equal(m_Packet, packet);
            };
            m_Dispatcher.RegisterStateMachine(eventHandler, eventHandler.stateMachine);
            m_Dispatcher.RegisterPacketHandler(eventHandler.NonStaticHandler);
            m_Dispatcher.Dispatch(null, m_Packet);
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
            m_Dispatcher.RegisterStateMachine(eventHandler, eventHandler.stateMachine);
            m_Dispatcher.RegisterPacketHandler(eventHandler.NonStaticHandler);
            m_Dispatcher.Dispatch(null, m_Packet);
            Assert.True(wasCalled);

            // Unregister
            wasCalled = false;
            m_Dispatcher.UnregisterPacketHandlers(eventHandler);
            m_Dispatcher.Dispatch(null, m_Packet);
            Assert.False(wasCalled);
        }
    }
}
