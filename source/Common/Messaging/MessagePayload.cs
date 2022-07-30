using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.PlayerServices;

namespace Common
{
    [Serializable]
    [ProtoContract]
    public readonly struct MessagePayload<T>
    {
        [ProtoMember(1)]
        public string Who { get; }
        [ProtoMember(2)]
        public T What { get; }
        [ProtoMember(3)]
        public DateTime When { get; }

        public MessagePayload(T payload, object source)
        {
            Who = source.ToString(); What = payload; When = DateTime.UtcNow;
        }
    }
}
