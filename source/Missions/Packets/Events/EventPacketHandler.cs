using Common;
using Common.Messaging;
using LiteNetLib;
using Missions.Network;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Reflection;

namespace Missions.Packets.Events
{
    internal class EventPacketHandler : IPacketHandler
    {
        public PacketType PacketType => PacketType.Event;

        public IMessageBroker MessageBroker { get; }

        public EventPacketHandler(IMessageBroker messageBroker)
        {
            MessageBroker = messageBroker;
        }


        private static readonly MethodInfo Publish = typeof(IMessageBroker).GetMethod(nameof(IMessageBroker.Publish));
        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            EventPacket convertedPacket = (EventPacket)packet;

            object @event = convertedPacket.Event;

            Publish.MakeGenericMethod(@event.GetType()).Invoke(MessageBroker, new object[] { peer, @event });
        }

        public void HandlePeerDisconnect(NetPeer peer, DisconnectInfo reason)
        {
            throw new NotImplementedException();
        }
    }

    [ProtoContract]
    public readonly struct EventPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

        public PacketType PacketType => PacketType.Event;

        [ProtoMember(1)]
        public object Event { get; }

        public EventPacket(IMessagePayload payload)
        {
            if (RuntimeTypeModel.Default.IsDefined(payload.GetType()) == false)
            {
                throw new ArgumentException($"Type {payload.GetType().Name} is not serializable.");
            }

            Event = payload.What;
        }

        public EventPacket(object @event)
        {
            if (RuntimeTypeModel.Default.IsDefined(@event.GetType()) == false)
            {
                throw new ArgumentException($"Type {@event.GetType().Name} is not serializable.");
            }

            Event = @event;
        }
    }
}
