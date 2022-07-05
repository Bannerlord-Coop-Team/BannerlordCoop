using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    [ProtoContract]
    public class BoardGameMoveEvent
    {
        [ProtoMember(1)]
        public int fromIndex { get; set; }

        [ProtoMember(2)]
        public int toIndex { get; set; }
    }
}
