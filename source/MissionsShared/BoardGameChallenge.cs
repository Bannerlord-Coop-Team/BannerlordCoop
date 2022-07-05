using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    [ProtoContract]
    public class BoardGameChallenge
    {
        [ProtoMember(1)]
        public bool ChallengeRequest { get; set; }

        [ProtoMember(2)]
        public bool ChallengeResponse { get; set; }

        [ProtoMember(3)]
        public string OtherAgentId { get; set; }

    }
}
