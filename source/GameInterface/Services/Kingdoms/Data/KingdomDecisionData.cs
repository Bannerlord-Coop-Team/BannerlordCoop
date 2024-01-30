using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    [ProtoInclude(0, nameof(DeclareWarDecisionData))]
    public class KingdomDecisionData
    {
        [ProtoMember(1)]
        public string ProposerClanId { get; }
        [ProtoMember(2)]
        public int TriggerTime { get; }
        [ProtoMember(3)]
        public bool IsEnforced { get; }
        [ProtoMember(4)]
        public bool NotifyPlayer { get; }
        [ProtoMember(5)]
        public bool PlayerExamined { get; }

        public KingdomDecisionData(string proposedClanId, int triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined)
        {
            ProposerClanId = proposedClanId;
            TriggerTime = triggerTime;
            IsEnforced = isEnforced;
            NotifyPlayer = notifyPlayer;
            PlayerExamined = playerExamined;
        }
    }
}
