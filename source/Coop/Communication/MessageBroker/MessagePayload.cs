using System;
using ProtoBuf;

namespace Coop.Communication.MessageBroker
{
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
}