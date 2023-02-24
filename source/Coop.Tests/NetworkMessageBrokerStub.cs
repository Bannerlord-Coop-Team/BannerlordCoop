using Common.Messaging;
using Common.Network;
using Coop.Tests.Stubs;
using LiteNetLib;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Coop.Tests
{
    public class NetworkMessageBrokerStub : MessageBrokerStub, INetworkMessageBroker
    {
        protected readonly Dictionary<Type, List<Delegate>> _testSubscribers = new Dictionary<Type, List<Delegate>>();

        public void PublishNetworkEvent(INetworkEvent networkEvent)
        {
            PublishNetworkEvent(null, networkEvent);
        }

        public void PublishNetworkEvent(NetPeer peer, INetworkEvent networkEvent)
        {
            Type type = networkEvent.GetType();
            if (!_testSubscribers.ContainsKey(type))
            {
                return;
            }

            var delegates = _testSubscribers[type];
            if (delegates == null || delegates.Count == 0) return;

            Type payloadType = typeof(MessagePayload<>).MakeGenericType(type);
            ConstructorInfo MessagePayload_Ctor = payloadType.GetConstructor(new Type[] { typeof(object), type });

            object payload = MessagePayload_Ctor.Invoke(new object[] { peer, networkEvent });
            foreach (var handler in delegates)
            {
                handler.DynamicInvoke(new object[] { payload });
            }
        }

        public void ReceiveNetworkEvent<T>(NetPeer peer, T networkEvent) where T : INetworkEvent
        {
            base.Publish(peer, networkEvent);
        }

        public override void Publish<T>(object source, T message)
        {
            if (typeof(INetworkEvent).IsAssignableFrom(typeof(T))) throw new ArgumentException(
                $"Attempting to publish an external message internally.");
            base.Publish(source, message);
        }

        /// <summary>
        /// Subscribes to a network events that would be sent over the
        /// network.
        /// </summary>
        /// <typeparam name="T">Type to unsub from</typeparam>
        /// <param name="subscription"></param>
        public void TestNetworkSubscribe<T>(Action<MessagePayload<T>> subscription) where T : INetworkEvent
        {
            var delegates = _testSubscribers.ContainsKey(typeof(T)) ?
                            _testSubscribers[typeof(T)] : new List<Delegate>();
            if (!delegates.Contains(subscription))
            {
                delegates.Add(subscription);
            }
            _testSubscribers[typeof(T)] = delegates;
        }

        /// <summary>
        /// Unsubscribes from network events that would be sent over the
        /// network.
        /// </summary>
        /// <typeparam name="T">Type to unsub from</typeparam>
        /// <param name="subscription"></param>
        public void TestNetworkUnsubscribe<T>(Action<MessagePayload<T>> subscription) where T : INetworkEvent
        {
            if (!_testSubscribers.ContainsKey(typeof(T))) return;
            var delegates = _testSubscribers[typeof(T)];
            if (delegates.Contains(subscription))
                delegates.Remove(subscription);
            if (delegates.Count == 0)
                _testSubscribers.Remove(typeof(T));
        }
    }
}