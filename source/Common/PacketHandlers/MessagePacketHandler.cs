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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
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

        protected static readonly MethodInfo Publish = typeof(IMessageBroker).GetMethod(nameof(IMessageBroker.Publish));
        public virtual void HandlePacket(NetPeer peer, IPacket packet)
        {
            MessagePacket convertedPacket = (MessagePacket)packet;

            var networkEvent = serializer.Deserialize<IMessage>(convertedPacket.Data);

            PublishEvent(peer, networkEvent);
        }
        private readonly ConcurrentDictionary<Type, Action<IMessageBroker, NetPeer, IMessage>> publishFunctionCache = new();

        internal virtual void PublishEvent(NetPeer peer, IMessage message)
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            var messageType = message.GetType();
            var publishInvoker = GetPublishInvoker(messageType);

            try
            {
                publishInvoker(messageBroker, peer, message);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                Logger.Error(ex.InnerException, "PublishEvent failed for {MessageType}", messageType);
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "PublishEvent failed for {MessageType}", messageType);
                throw;
            }
        }

        private Action<IMessageBroker, NetPeer, IMessage> GetPublishInvoker(Type messageType)
        {
            return publishFunctionCache.GetOrAdd(messageType, CreatePublishInvoker);
        }

        private Action<IMessageBroker, NetPeer, IMessage> CreatePublishInvoker(Type messageType)
        {
            var genericPublish = Publish.MakeGenericMethod(messageType);

            var brokerParam = Expression.Parameter(typeof(IMessageBroker), "broker");
            var peerParam = Expression.Parameter(typeof(NetPeer), "peer");
            var messageParam = Expression.Parameter(typeof(IMessage), "message");

            var call = Expression.Call(
                brokerParam,
                genericPublish,
                peerParam,
                Expression.Convert(messageParam, messageType));

            return Expression
                .Lambda<Action<IMessageBroker, NetPeer, IMessage>>(
                    call,
                    brokerParam,
                    peerParam,
                    messageParam)
                .Compile();
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public readonly struct MessagePacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

        public PacketType PacketType => PacketType.Message;

        [ProtoMember(1)]
        public readonly byte[] Data;

        public MessagePacket(byte[] data)
        {
            Data = data;
        }
    }
}
