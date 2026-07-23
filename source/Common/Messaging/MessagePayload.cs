using ProtoBuf;
using System;

namespace Common.Messaging
{
    [Serializable]
    [ProtoContract(SkipConstructor = true)]
    public sealed record MessagePayload<T> where T : IMessage
    {
        public object Who { get; set; }
        [ProtoMember(1)]
        public T What { get; }
        [ProtoMember(2)]
        public DateTime When { get; }

        public MessagePayload(object source, T payload)
        {
            Who = source; What = payload; When = DateTime.UtcNow;
        }

        private MessagePayload(object who, object what, DateTime when)
        {
            Who = who;
            What = (T)what;
            When = when;
        }

        public static implicit operator MessagePayload<IMessage>(MessagePayload<T> payload)
        {
            return new MessagePayload<IMessage>(payload.Who, payload.What, payload.When);
        }

        public static explicit operator MessagePayload<T>(MessagePayload<IMessage> payload)
        {
            return new MessagePayload<T>(payload.Who, payload.What, payload.When);
        }
    }
}
