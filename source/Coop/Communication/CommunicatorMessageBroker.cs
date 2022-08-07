using System;
using System.Collections.Generic;
using Coop.Communication.MessageBroker;
using Coop.Communication.PacketHandlers;
using LiteNetLib;
using ProtoBuf;

namespace Coop.Communication
{
    public class CommunicatorMessageBroker : IMessageBroker
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();

        private readonly IPacketManager _packetManager;

        public CommunicatorMessageBroker(IPacketManager packetManager)
        {
            _packetManager = packetManager;
        }
        
        /// <summary>
        ///     Call an event based on the type of the message.
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="message">Message event</param>
        /// <param name="scope">Scope of the message</param>
        /// <typeparam name="T">Type of the message</typeparam>
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

        /// <summary>
        ///     Register an delegate based on the type of T so that it get called
        ///     when we receive an event of T.
        /// </summary>
        /// <param name="subscriber">Delegate method</param>
        /// <typeparam name="T">Type of event subscribing</typeparam>
        public void Subscribe<T>(Action<MessagePayload<T>> subscriber)
        {
            if(!_subscribers.ContainsKey(typeof(T)))
                _subscribers.Add(typeof(T), new List<Delegate>());
            
            _subscribers[typeof(T)].Add(subscriber);
        }

        /// <summary>
        ///     Unregister an event delegate.
        /// </summary>
        /// <param name="subscriber"></param>
        /// <typeparam name="T"></typeparam>
        public void Unsubscribe<T>(Action<MessagePayload<T>> subscriber)
        {
            if (_subscribers.TryGetValue(typeof(T), out var subscribers)) 
                subscribers.Remove(subscriber);
        }

        public void Dispose()
        {
            _subscribers.Clear();
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
