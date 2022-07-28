using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.PlayerServices;

namespace Common
{
    [Serializable]
    public readonly struct MessagePayload<T>
    {
        public object Who { get; }
        public T What { get; }
        public DateTime When { get; }

        public MessagePayload(T payload, object source)
        {
            Who = source; What = payload; When = DateTime.UtcNow;
        }
    }
}
