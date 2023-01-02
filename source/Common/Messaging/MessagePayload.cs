using ProtoBuf;
using System;

namespace Common
{
    public interface IMessagePayload
    { 
        object Who { get; }
        object What { get; }
        DateTime When { get; }

    }

    [Serializable]
    [ProtoContract(SkipConstructor = true)]
    public struct MessagePayload<T> : IMessagePayload
    {
        public object Who { get; set; }
        [ProtoMember(1)]
        public T What { get; }
        [ProtoMember(2)]
        public DateTime When { get; }

        object IMessagePayload.What => What;

        public MessagePayload(T payload, object source)
        {
            Who = source; What = payload; When = DateTime.UtcNow;
        }
    }
}
