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
    /// <summary>
    /// Class for serializing <see cref="SettlementClaimantPreliminaryDecision"> class.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class SettlementClaimantPreliminaryDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo SettlementField = typeof(SettlementClaimantPreliminaryDecision).GetField(nameof(SettlementClaimantPreliminaryDecision.Settlement), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo OwnerClanField = typeof(SettlementClaimantPreliminaryDecision).GetField("_ownerClan", BindingFlags.Instance | BindingFlags.NonPublic);
        
        [ProtoMember(1)]
        public string SettlementId { get; }
        [ProtoMember(2)]
        public string OwnerClanId { get; }
        public SettlementClaimantPreliminaryDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string settlementId, string ownerClanId) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            SettlementId = settlementId;
            OwnerClanId = ownerClanId;
        }

        /// <inheritdoc/>
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
            SettlementField.SetValue(settlementClaimantPreliminaryDecision, settlement);
            OwnerClanField.SetValue(settlementClaimantPreliminaryDecision, ownerClan);
            kingdomDecision = settlementClaimantPreliminaryDecision;
            return true;
        }
    }
}
