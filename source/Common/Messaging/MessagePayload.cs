using ProtoBuf;
using System;

namespace Common.Messaging
{
    [Serializable]
    [ProtoContract(SkipConstructor = true)]
    public struct MessagePayload<T>
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
    }
}
