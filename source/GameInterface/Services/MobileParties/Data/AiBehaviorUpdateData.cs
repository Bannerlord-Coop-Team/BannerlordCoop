using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Data
{
    [ProtoContract(SkipConstructor = true)]
    public record AiBehaviorUpdateData
    {
        [ProtoMember(1)]
        public string PartyId { get; }

        [ProtoMember(2)]
        public AiBehavior Behavior { get; }

        [ProtoMember(3)]
        public bool HasTarget { get; }

        [ProtoMember(4)]
        public string TargetId { get; }

        [ProtoMember(5)]
        public Vec2 TargetPoint { get; }

        public AiBehaviorUpdateData(string partyId, AiBehavior aiBehavior, bool hasTarget, string targetId, Vec2 targetPoint)
        {
            PartyId = partyId;
            Behavior = aiBehavior;
            HasTarget = hasTarget;
            TargetId = targetId;
            TargetPoint = targetPoint;
        }
    }
}
