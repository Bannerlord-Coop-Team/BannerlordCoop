using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    [ProtoContract]
    public struct BoardGameChallengeRequest
    {
        [ProtoMember(1)]
        public bool ChallengeRequest;

        [ProtoMember(2)]
        public bool ChallengeResponse;

        [ProtoMember(3)]
        public string SenderAgentId;

        [ProtoMember(4)]
        public string OtherAgentId;


    }
}
