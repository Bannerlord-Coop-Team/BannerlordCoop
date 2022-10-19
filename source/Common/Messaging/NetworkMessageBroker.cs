using System;
using System.Collections.Generic;

namespace Common.Messaging
{
    public interface INetworkMessageBroker
    {
        void Publish<T>(object source, T message) where T : INetworkMessage;
        void PublishToAll<T>(object source, T message) where T : INetworkSendToAllMessage;
    }

    public class NetworkMessageBroker : INetworkMessageBroker
    {
        protected readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();

        public virtual void Publish<T>(object source, T message) where T : INetworkMessage
        {

        }

        public void PublishToAll<T>(object source, T message) where T : INetworkSendToAllMessage
        {

        }
    }
}
