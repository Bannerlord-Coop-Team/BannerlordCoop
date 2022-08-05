using Common.MessageBroker;
using Coop.Mod.PacketHandlers;
using LiteNetLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Messages
{
    public class MessageBroker : IMessageBroker
    {
        private static Lazy<MessageBroker> instance = new Lazy<MessageBroker>();
        public static MessageBroker Instance => instance.Value;

        private readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();

        private readonly IPacketManager _packetManager;

        public MessageBroker(IPacketManager packetManager)
        {
            _packetManager = packetManager;
        }



        public void Publish<T>(object sender, T message, MessageScope scope = MessageScope.Internal)
        {
            if(scope == MessageScope.Internal)
            {
                MessagePayload<T> payload = new MessagePayload<T>(sender, message);
            }
            else if(scope == MessageScope.External)
            {
                // TODO
            }
        }

        public void Subscribe<T>(Action<MessagePayload<T>> subscriber)
        {
            if(_subscribers.TryGetValue(typeof(T), out var subscribers))
            {
                subscribers.Add(subscriber);
            }
        }

        public void Unsubscribe<T>(Action<MessagePayload<T>> subscriber)
        {
            if (_subscribers.TryGetValue(typeof(T), out var subscribers))
            {
                subscribers.Remove(subscriber);
            }
        }
    }

    public class MessageBrokerPacketHandler : IPacketHandler
    {
        public PacketType PacketType => PacketType.Event;

        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            throw new NotImplementedException();
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public readonly struct MessagePacket<T> : IPacket
    {
        public PacketType Type => PacketType.Event;

        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableSequenced;

        [ProtoMember(1)]
        public T Payload { get; }

        public MessagePacket(T payload)
        {
            Payload = payload;
        }
    }
}
