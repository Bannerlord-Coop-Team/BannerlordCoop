using GameInterface.Services.Kingdoms.Data.IFactionDatas;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class DeclareWarDecisionData : KingdomDecisionData
    {
        [ProtoMember(1)]
        public IFactionData FactionToDeclareWarOn { get; }

        public DeclareWarDecisionData(string proposedClanId, int triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, IFactionData factionToDeclareWarOn) :base(proposedClanId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            FactionToDeclareWarOn = factionToDeclareWarOn;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager,out KingdomDecision kingdomDecision)
        {
            if (!objectManager.TryGetObject(ProposerClanId, out Clan clan))
            {
                kingdomDecision = null;
                return false;
            }

            if (!FactionToDeclareWarOn.TryGetIFaction(objectManager, out IFaction factionToDeclareWarOn))
            {
                kingdomDecision = null;
                return false;
            }

            kingdomDecision = new DeclareWarDecision(clan, factionToDeclareWarOn);
            SetKingdomDecisionProperties(kingdomDecision);
            return true;
        }
    }
}
