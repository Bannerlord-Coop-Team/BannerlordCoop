using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class DeclareWarDecisionData : KingdomDecisionData
    {
        [ProtoMember(1)]
        public string FactionToDeclareWarOnId { get; }

        public DeclareWarDecisionData(string proposedClanId, int triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string factionToDeclareWarOnId) :base(proposedClanId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            FactionToDeclareWarOnId = factionToDeclareWarOnId;

        }
    }
}
