using Common.Extensions;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class SettlementClaimantPreliminaryDecisionData : KingdomDecisionData
    {
        private static Action<SettlementClaimantPreliminaryDecision, Settlement> SetSettlement = typeof(SettlementClaimantPreliminaryDecision).GetField(nameof(SettlementClaimantPreliminaryDecision.Settlement), BindingFlags.Instance | BindingFlags.Public).BuildUntypedSetter<SettlementClaimantPreliminaryDecision, Settlement>();
        private static Action<SettlementClaimantPreliminaryDecision, Clan> SetOwnerClan = typeof(SettlementClaimantPreliminaryDecision).GetField("_ownerClan", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedSetter<SettlementClaimantPreliminaryDecision, Clan>();
        
        [ProtoMember(1)]
        public string SettlementId { get; }
        [ProtoMember(2)]
        public string OwnerClanId { get; }
        public SettlementClaimantPreliminaryDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string settlementId, string ownerClanId) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            SettlementId = settlementId;
            OwnerClanId = ownerClanId;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !objectManager.TryGetObject(SettlementId, out Settlement settlement) ||
                !objectManager.TryGetObject(OwnerClanId, out Clan ownerClan))
            {
                kingdomDecision = null;
                return false;
            }

            SettlementClaimantPreliminaryDecision settlementClaimantPreliminaryDecision = (SettlementClaimantPreliminaryDecision)FormatterServices.GetUninitializedObject(typeof(SettlementClaimantPreliminaryDecision));
            SetKingdomDecisionProperties(settlementClaimantPreliminaryDecision, proposerClan, kingdom);
            SetSettlement(settlementClaimantPreliminaryDecision, settlement);
            SetOwnerClan(settlementClaimantPreliminaryDecision, ownerClan);
            kingdomDecision = settlementClaimantPreliminaryDecision;
            return true;
        }
    }
}
