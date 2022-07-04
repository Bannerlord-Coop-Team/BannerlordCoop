using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    [ProtoContract]
    public class FromServerClientsAgents
    {
        [ProtoMember(1)]
        Dictionary<int, List<string>> agents;
    }
}
