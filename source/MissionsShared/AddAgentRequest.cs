using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    [ProtoContract]
    public struct AddAgentRequest
    {
        [ProtoMember(1)]
        public int AgentIndex;
    }
}
