using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ProtoBuf;

namespace MissionsShared
{
    [ProtoContract]
    public class FromServerTickMessage
    {
        [ProtoMember(1)]
        public List<FromServerTickPayload> ClientTicks { get; set; }

        public FromServerTickMessage()
        {
            ClientTicks = new List<FromServerTickPayload>();
        }


    }


}
