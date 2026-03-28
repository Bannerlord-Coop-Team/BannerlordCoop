using GameInterface.Services.MobileParties.Handlers;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Data
{
    /// <summary>
    /// Contains the data used for <see cref="MobilePartyAi"/> behavior synchronisation.
    /// </summary>
    /// <seealso cref="MobilePartyBehaviorHandler"/>
    [ProtoContract(SkipConstructor = true)]
    public struct PartyBehaviorUpdateData
    {
        [ProtoMember(1)]
        public readonly string MobilePartyId;

        [ProtoMember(2)]
        public readonly AiBehavior NewAiBehavior;

        [ProtoMember(3)]
        public readonly string InteractablePointId;

        [ProtoMember(4)]
        public readonly CampaignVec2 BestTargetPoint;

        [ProtoMember(5)]
        public readonly bool HasTarget;

        [ProtoMember(8)]
        public CampaignVec2 PartyPosition { get; set; }

        public PartyBehaviorUpdateData(
            string mobilePartyId,
            AiBehavior newAiBehavior,
            string interactablePointId,
            CampaignVec2 bestTargetPoint,
            bool hasTarget,
            CampaignVec2 partyPosition)
        {
            MobilePartyId = mobilePartyId;
            NewAiBehavior = newAiBehavior;
            InteractablePointId = interactablePointId;
            BestTargetPoint = bestTargetPoint;
            HasTarget = hasTarget;
            PartyPosition = partyPosition;
        }
    }
}