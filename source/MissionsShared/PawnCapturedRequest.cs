using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    [ProtoContract]
    public struct PawnCapturedRequest
    {
        [ProtoMember(1)]
        public int FromIndex;
    }
}
