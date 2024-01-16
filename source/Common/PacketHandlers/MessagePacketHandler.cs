using Common.Extensions;
using Common.Logging;
using Common.Logging.Attributes;
using Common.Messaging;
using Common.Serialization;
using LiteNetLib;
using ProtoBuf;
using ProtoBuf.Meta;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Common.PacketHandlers
{
    public interface IMessagePacketHandler : IPacketHandler { }

    public class MessagePacketHandler : IMessagePacketHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<MessagePacketHandler>();

        public PacketType PacketType => PacketType.Message;

        private readonly IMessageBroker _messageBroker;
        private readonly IPacketManager _packetManager;

        public MessagePacketHandler(IMessageBroker messageBroker, IPacketManager packetManager)
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
            MessagePacket convertedPacket = (MessagePacket)packet;

            IMessage networkEvent = convertedPacket.Message;

            if (networkEvent.GetType().GetCustomAttribute<BatchLogMessageAttribute>() == null)
            {
                Logger.Information("Received network event from {Peer} of {EventType}", peer.EndPoint, networkEvent.GetType().Name);
            }

            PublishEvent(peer, networkEvent);
        }
        private Dictionary<Type, Action<IMessageBroker, object, object>> publishFunctionLookup = new Dictionary<Type, Action<IMessageBroker, object, object>>();
        internal virtual void PublishEvent(NetPeer peer, IMessage message)
        {
            var msgType = message.GetType();
            if (publishFunctionLookup.TryGetValue(msgType, out var action))
            {
                action.Invoke(_messageBroker, peer, message);
            }
            else
            {
                var castedPublish = Publish.MakeGenericMethod(message.GetType());
                publishFunctionLookup.Add(msgType, 
                    (messageBrokerParam, peerParam, messageParam) => castedPublish.Invoke(messageBrokerParam, new object[] { peerParam, messageParam }));

                castedPublish.Invoke(_messageBroker, new object[] { peer, message });
            }
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public class MessagePacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

        public PacketType PacketType => PacketType.Message;

        public IMessage Message
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

        public MessagePacket(IMessage message)
        {
            if (RuntimeTypeModel.Default.IsDefined(message.GetType()) == false)
            {
                throw new ArgumentException($"Type {message.GetType().Name} is not serializable.");
            }

            Message = message;
        }
    }
}
