using Common;
using Common.Messaging;
using Common.Serialization;
using Coop.NetImpl.LiteNet;
using LiteNetLib;
using LiteNetLib.Utils;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Mission.Network
{
    public class MessageBroker : IMessageBroker
    {
        private HashSet<Type> serializableTypesSet = new HashSet<Type>();


        private readonly Dictionary<Type, List<Delegate>> _subscribers;
        private readonly LiteNetP2PClient _client;

        public MessageBroker(LiteNetP2PClient client)
        {
            _client = client;
            _subscribers = new Dictionary<Type, List<Delegate>>();

            _client.DataRecieved += OnRecieve;
        }

        public void Publish<T>(object source, T message)
        {
            if (message == null || source == null)
                return;
            
            if (!serializableTypesSet.Contains(source.GetType()) &&
                !source.GetType().IsSerializable &&
                !source.GetType().GetCustomAttributes(false).Contains(typeof(ProtoContractAttribute)))
            {
                string msg = $"{source.GetType()} is not marked as serializable or is not a proto contract.";
                throw new InvalidOperationException(msg);
            }
            else
            {
                serializableTypesSet.Add(source.GetType());
            }

            var payload = new MessagePayload<T>(message, source.ToString());


            NetDataWriter writer = LiteNetPackager.Pack(payload);


            _client.SendAll(writer, DeliveryMethod.ReliableOrdered);
        }

        public void OnRecieve(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            object payload = LiteNetPackager.Unpack(reader);

            Type type = payload.GetType();
            if (type.GetGenericTypeDefinition() != typeof(MessagePayload<>))
            {
                throw new InvalidCastException($"{payload.GetType()} is not of type {typeof(MessagePayload<>)}");
            }

            Type T = type.GetProperty("What").PropertyType;

            if (!_subscribers.ContainsKey(T))
            {
                return;
            }

            var delegates = _subscribers[T];
            if (delegates == null || delegates.Count == 0) return;

            foreach (var handler in delegates)
            {
                Task.Factory.StartNew(() => handler.Method.Invoke(handler.Target, new object[] { payload }));
            }
        }

        public void Subscribe<T>(Action<MessagePayload<T>> subscription)
        {
            var delegates = _subscribers.ContainsKey(typeof(T)) ?
                            _subscribers[typeof(T)] : new List<Delegate>();
            if (!delegates.Contains(subscription))
            {
                delegates.Add(subscription);
            }
            _subscribers[typeof(T)] = delegates;
        }

        public void Unsubscribe<T>(Action<MessagePayload<T>> subscription)
        {
            if (!_subscribers.ContainsKey(typeof(T))) return;
            var delegates = _subscribers[typeof(T)];
            if (delegates.Contains(subscription))
                delegates.Remove(subscription);
            if (delegates.Count == 0)
                _subscribers.Remove(typeof(T));
        }

        public void Dispose()
        {
            _subscribers?.Clear();
        }
    }
}
