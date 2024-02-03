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
    public class SettlementClaimantDecisionData : KingdomDecisionData
    {
        [ProtoMember(1)]
        public string SettlementId { get; }
        [ProtoMember(2)]
        public string CapturerHeroId { get; }
        [ProtoMember(3)]
        public string ClanToExcludeId { get; }

        public SettlementClaimantDecisionData(string proposedClanId, int triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string settlementId, string capturerHeroId, string clanToExcludeId) : base(proposedClanId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            SettlementId = settlementId;
            CapturerHeroId = capturerHeroId;
            ClanToExcludeId = clanToExcludeId;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!objectManager.TryGetObject(ProposerClanId, out Clan proposerClan) || 
                !objectManager.TryGetObject(ClanToExcludeId, out Clan clanToExclude) ||
                !objectManager.TryGetObject(SettlementId, out Settlement settlement) ||
                !objectManager.TryGetObject(CapturerHeroId, out Hero capturerHero))
            {
                kingdomDecision = null;
                return false;
            }

            kingdomDecision = new SettlementClaimantDecision(proposerClan, settlement, capturerHero ,clanToExclude);
            SetKingdomDecisionProperties(kingdomDecision);
            return true;
        }
    }
}
