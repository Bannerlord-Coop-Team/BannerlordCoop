using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.Kingdoms.Handlers;

namespace GameInterface.Services.MobileParties.Data
{
    /// <summary>
    /// Contains the data used for <see cref="MobilePartyAi"/> behavior synchronisation.
    /// </summary>
    /// <seealso cref="MobilePartyBehaviorHandler"/>
    [ProtoContract(SkipConstructor = true)]
    public record PartyBehaviorUpdateData
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

        public PartyBehaviorUpdateData(string partyId, AiBehavior aiBehavior, bool hasTarget, string targetId, Vec2 targetPoint)
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