using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class KingdomPolicyDecisionData : KingdomDecisionData
    {
        [ProtoMember(1)]
        public string PolicyObjectId { get; }

        [ProtoMember(2)]
        public bool IsInvertedDecision { get; }

        public KingdomPolicyDecisionData(string proposedClanId, int triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string policyObjectId, bool isInvertedDecision) : base(proposedClanId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            PolicyObjectId = policyObjectId;
            IsInvertedDecision = isInvertedDecision;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!objectManager.TryGetObject(ProposerClanId, out Clan proposerClan))
            {
                kingdomDecision = null;
                return false;
            }

            if (!objectManager.TryGetObject(PolicyObjectId, out PolicyObject policyObject))
            {
                kingdomDecision = null;
                return false;
            }

            kingdomDecision = new KingdomPolicyDecision(proposerClan, policyObject, IsInvertedDecision);
            SetKingdomDecisionProperties(kingdomDecision);
            return true;
        }
    }
}
