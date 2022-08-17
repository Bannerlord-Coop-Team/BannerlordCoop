using System;
using ProtoBuf;

namespace Common.Messaging
{
    public readonly struct MessagePayload<T>
    {
        public object Who { get; }
        public T What { get; }
        public DateTime When { get; }

        public MessagePayload(object who, T what)
        {
            Who = who;
            What = what;
            When = DateTime.UtcNow;
        }
    }
}