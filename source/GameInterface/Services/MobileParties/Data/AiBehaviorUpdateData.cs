using ProtoBuf;
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
        public float TargetPointX { get; }

        [ProtoMember(6)]
        public float TargetPointY { get; }

        public AiBehaviorUpdateData(string partyId, AiBehavior aiBehavior, bool hasTarget, string targetId, Vec2 targetPoint)
        {
            PartyId = partyId;
            Behavior = aiBehavior;
            HasTarget = hasTarget;
            TargetId = targetId;
            TargetPointX = targetPoint.X;
            TargetPointY = targetPoint.Y;
        }
    }
}
