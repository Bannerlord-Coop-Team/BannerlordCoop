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
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Common.PacketHandlers
{
    public interface IMessagePacketHandler : IPacketHandler { }

    public class MessagePacketHandler : IMessagePacketHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<MessagePacketHandler>();

        public PacketType PacketType => PacketType.Message;

        private readonly IMessageBroker messageBroker;
        private readonly IPacketManager packetManager;
        private readonly ICommonSerializer serializer;

        public MessagePacketHandler(IMessageBroker messageBroker, IPacketManager packetManager, ICommonSerializer serializer)
        {
            this.messageBroker = messageBroker;
            this.packetManager = packetManager;
            this.serializer = serializer;
            this.packetManager.RegisterPacketHandler(this);
        }

        public virtual void Dispose()
        {
            packetManager.RemovePacketHandler(this);
        }

        protected static readonly MethodInfo Publish = typeof(IMessageBroker).GetMethods().First(method => method.Name == nameof(IMessageBroker.Publish) && method.GetParameters().Length == 3);
        public virtual void HandlePacket(NetPeer peer, IPacket packet)
        {
            MessagePacket convertedPacket = (MessagePacket)packet;

            var networkEvent = serializer.Deserialize<IMessage>(convertedPacket.Data);

            PublishEvent(peer, networkEvent, packet.SubKey);
        }
        private Dictionary<string, Action<IMessageBroker, object, object, string>> publishFunctionCache = new();
        internal virtual void PublishEvent(NetPeer peer, IMessage message, string subKey)
        {
            var msgType = message.GetType();
            if (publishFunctionCache.TryGetValue(msgType.FullName, out var action))
            {
                action.Invoke(messageBroker, peer, message, subKey);
            }
            else
            {
                var castedPublish = Publish.MakeGenericMethod(message.GetType());
                publishFunctionCache.Add(msgType.FullName, 
                    (messageBrokerParam, peerParam, messageParam, subKeyParam) => castedPublish.Invoke(messageBrokerParam, new object[] { peerParam, messageParam, subKeyParam }));

                castedPublish.Invoke(messageBroker, new object[] { peer, message, subKey });
            }
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public readonly struct MessagePacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

        public PacketType PacketType => PacketType.Message;

        [ProtoMember(1)]
        public readonly byte[] Data;
        
        [ProtoMember(2)]
        public string SubKey { get; }

        public MessagePacket(byte[] data, string subKey = "")
        {
            Data = data;
            SubKey = subKey;
        }
    }
}
