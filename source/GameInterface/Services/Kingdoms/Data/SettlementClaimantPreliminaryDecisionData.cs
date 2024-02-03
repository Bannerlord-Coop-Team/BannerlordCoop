using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class SettlementClaimantPreliminaryDecisionData : KingdomDecisionData
    {
        [ProtoMember(1)]
        public string SettlementId { get; }
        public SettlementClaimantPreliminaryDecisionData(string proposedClanId, int triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string settlementId) : base(proposedClanId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            SettlementId = settlementId;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!objectManager.TryGetObject(ProposerClanId, out Clan proposerClan) ||
                !objectManager.TryGetObject(SettlementId, out Settlement settlement))
            {
                kingdomDecision = null;
                return false;
            }

            kingdomDecision = new SettlementClaimantPreliminaryDecision(proposerClan, settlement);
            SetKingdomDecisionProperties(kingdomDecision);
            return true;
        }
    }
}
