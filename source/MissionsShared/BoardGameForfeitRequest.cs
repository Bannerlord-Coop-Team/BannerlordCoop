using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    [ProtoContract]
    public struct BoardGameForfeitRequest
    {
        [ProtoMember(1)]
        public string OtherAgentId;
    }
}
