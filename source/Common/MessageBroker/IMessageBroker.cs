using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.MessageBroker
{
    public enum MessageScope
    {
        Invalid,
        /// <summary>
        /// Message is only broadcast internally (not across the network)
        /// </summary>
        Internal,
        /// <summary>
        /// Message is only broadcast across the network (not internally)
        /// </summary>
        External
    }
    [ProtoContract]
    public readonly struct MessagePayload<T>
    {
        [ProtoMember(1)]
        public MessageScope Scope { get; }
        [ProtoMember(2)]
        public object Who { get; }
        [ProtoMember(3)]
        public T What { get; }
        [ProtoMember(4)]
        public DateTime When { get; }

        public MessagePayload(object who, T what, MessageScope scope = MessageScope.Internal)
        {
            Who = who;
            What = what;
            When = DateTime.UtcNow;
            Scope = scope;
        }


    }
    public interface IMessageBroker
    {
        void Subscribe<T>(Action<MessagePayload<T>> subscriber);
        void Unsubscribe<T>(Action<MessagePayload<T>> subscriber);
        void Publish<T>(object sender, T payload);
    }
}
