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
    [ProtoContract(SkipConstructor = true)]
    public struct MessagePayload<T>
    {
        public object Who { get; set; }
        [ProtoMember(1)]
        public T What { get; }
        [ProtoMember(2)]
        public DateTime When { get; }

        public MessagePayload(T payload, object source)
        {
            Who = source; What = payload; When = DateTime.UtcNow;
        }
    }
}
