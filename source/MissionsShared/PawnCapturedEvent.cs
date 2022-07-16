using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    [ProtoContract]
    public class PawnCapturedEvent
    {
        [ProtoMember(1)]
        public int fromIndex { get; set; }
    }
}
