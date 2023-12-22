using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using LiteNetLib;
using ProtoBuf;
using ProtoBuf.Meta;
using Serilog;
using System;
using System.Reflection;

namespace Common.PacketHandlers
{
    public interface IEventPacketHandler : IPacketHandler { }

    public class EventPacketHandler : IEventPacketHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<EventPacketHandler>();

        public PacketType PacketType => PacketType.Event;

        private readonly IMessageBroker _messageBroker;
        private readonly IPacketManager _packetManager;

        public EventPacketHandler(IMessageBroker messageBroker, IPacketManager packetManager)
        {
            _messageBroker = messageBroker;
            _packetManager = packetManager;

            _packetManager.RegisterPacketHandler(this);
        }

        public virtual void Dispose()
        {
            _packetManager.RemovePacketHandler(this);
        }

        protected static readonly MethodInfo Publish = typeof(IMessageBroker).GetMethod(nameof(IMessageBroker.Publish));
        public virtual void HandlePacket(NetPeer peer, IPacket packet)
        {
            EventPacket convertedPacket = (EventPacket)packet;

            IMessage networkEvent = convertedPacket.Event;

            if (networkEvent.GetType().GetCustomAttribute<DontLogMessageAttribute>() == null)
            {
                Logger.Information("Received network event from {Peer} of {EventType}", peer.EndPoint, networkEvent.GetType().Name);
            }

            PublishEvent(peer, networkEvent);
        }

        protected virtual void PublishEvent(NetPeer peer, IMessage message) 
        {
            Publish.MakeGenericMethod(message.GetType()).Invoke(_messageBroker, new object[] { peer, message });
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public class EventPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

        public PacketType PacketType => PacketType.Event;

        public IMessage Event
        {
            get
            {
                return (IMessage)ProtoBufSerializer.Deserialize(_message);
            }
            set
            {
                _message = ProtoBufSerializer.Serialize(value);
            }
        }

        [ProtoMember(1)]
        private byte[] _message;

        public EventPacket(IMessage message)
        {
            if (RuntimeTypeModel.Default.IsDefined(message.GetType()) == false)
            {
                throw new ArgumentException($"Type {message.GetType().Name} is not serializable.");
            }

            Event = message;
        }
    }
}
