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
    public class EventPacketHandler : IPacketHandler
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

        public void Dispose()
        {
            _packetManager.RemovePacketHandler(this);
        }

        private static readonly MethodInfo Publish = typeof(IMessageBroker).GetMethod(nameof(IMessageBroker.Publish));
        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            EventPacket convertedPacket = (EventPacket)packet;

            INetworkEvent @event = convertedPacket.Event;

            Logger.Information("Received network event from {Peer} of {EventType}", peer, @event.GetType());

            Publish.MakeGenericMethod(@event.GetType()).Invoke(_messageBroker, new object[] { peer, @event });
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public class EventPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

        public PacketType PacketType => PacketType.Event;

        public INetworkEvent Event
        {
            get
            {
                return (INetworkEvent)ProtoBufSerializer.Deserialize(_event);
            }
            set
            {
                _event = ProtoBufSerializer.Serialize(value);
            }
        }

        [ProtoMember(1)]
        private byte[] _event;

        public EventPacket(INetworkEvent @event)
        {
            if (RuntimeTypeModel.Default.IsDefined(@event.GetType()) == false)
            {
                throw new ArgumentException($"Type {@event.GetType().Name} is not serializable.");
            }

            Event = @event;
        }
    }
}
