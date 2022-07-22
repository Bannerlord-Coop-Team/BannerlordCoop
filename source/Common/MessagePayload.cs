using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.PlayerServices;

namespace Common
{
    public class MessagePayload<T>
    {
        public PlayerId who;
        public T what;
        public DateTime when;

    }
}
