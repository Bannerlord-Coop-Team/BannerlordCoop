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
        
        [ProtoMember(3)]
        public string SubKey { get; }

        public MessagePayload(object source, T payload, string subKey = "")
        {
            Who = source; What = payload; When = DateTime.UtcNow; SubKey = subKey;
        }

        private MessagePayload(object who, object what, DateTime when, string subKey = "")
        {
            Who = who;
            What = (T)what;
            When = when;
            SubKey = subKey;
        }

        public static implicit operator MessagePayload<IMessage>(MessagePayload<T> payload)
        {
            return new MessagePayload<IMessage>(payload.Who, payload.What, payload.When, payload.SubKey);
        }

        public static explicit operator MessagePayload<T>(MessagePayload<IMessage> payload)
        {
            return new MessagePayload<T>(payload.Who, payload.What, payload.When, payload.SubKey);
        }
    }
}
