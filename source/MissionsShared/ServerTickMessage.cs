using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ProtoBuf;

namespace MissionsShared
{
    [ProtoContract]
    public class ServerTickMessage
    {
        [ProtoMember(1)]
        public List<ServerTickPayload> ClientTicks { get; set; }

        public ServerTickMessage()
        {
            ClientTicks = new List<ServerTickPayload>();
        }


    }


}
