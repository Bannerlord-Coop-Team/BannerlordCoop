using Common;
using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using LiteNetLib;
using ProtoBuf;
using ProtoBuf.Meta;
using Serilog;
using System;
using System.Reflection;
using Missions.Services.Network;
using Common.PacketHandlers;

namespace Missions.Services.Network.PacketHandlers
{
    internal class EventPacketHandler : IPacketHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<EventPacketHandler>();

        public PacketType PacketType => PacketType.Event;

        private readonly IMessageBroker _messageBroker;
        private readonly LiteNetP2PClient _client;

        public EventPacketHandler(LiteNetP2PClient client, IMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;
            _client = client;
        }

        private static readonly MethodInfo Publish = typeof(IMessageBroker).GetMethod(nameof(IMessageBroker.Publish));
        public void HandlePacket(NetPeer peer, IPacket packet)
        {
            EventPacket convertedPacket = (EventPacket)packet;

            IMessage @event = convertedPacket.Event;

            Logger.Information("Received network event from {Peer} of {EventType}", peer, @event.GetType());

            Publish.MakeGenericMethod(@event.GetType()).Invoke(_messageBroker, new object[] { peer, @event });
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public class EventPacket : IMessage
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;



        public IMessage Event
        {
            get
            {
                return (IMessage)ProtoBufSerializer.Deserialize(_event);
            }
            set
            {
                _event = ProtoBufSerializer.Serialize(value);
            }
        }

        PacketType PacketType => PacketType.Event;

        [ProtoMember(1)]
        public byte[] _event;

        public EventPacket(IMessage @event)
        {
            if (RuntimeTypeModel.Default.IsDefined(@event.GetType()) == false)
            {
                throw new ArgumentException($"Type {@event.GetType().Name} is not serializable.");
            }

            Event = @event;
        }
    }
}