using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

namespace MissionsShared
{
    [ProtoContract]
    public class ServerTickPayload
    {

        [ProtoMember(1)]
        public int ClientId { get; set; }
        [ProtoMember(2)]
        public int AgentCount { get; set; }
        [ProtoMember(3)]
        public List<PlayerTickInfo> PlayerTick;

        public PlayerTickPayload()
        {
            PlayerTick = new List<PlayerTickInfo>();    
        }
    }
}
