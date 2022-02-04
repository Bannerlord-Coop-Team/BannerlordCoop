using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace MissionsShared
{
    [ProtoContract]
    public class FromClientTickMessage { 


        [ProtoMember(1)]
        public int AgentCount { get; set; } 
        [ProtoMember(2)]
        public List<PlayerTickInfo> AgentsTickInfo { get; set; }

    }
}
