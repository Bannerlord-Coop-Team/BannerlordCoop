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
    /// Class for serializing <see cref="SettlementClaimantDecision"> class.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class SettlementClaimantDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo SettlementField = typeof(SettlementClaimantDecision).GetField(nameof(SettlementClaimantDecision.Settlement), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo ClanToExcludeField = typeof(SettlementClaimantDecision).GetField(nameof(SettlementClaimantDecision.ClanToExclude), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo CapturerHeroField = typeof(SettlementClaimantDecision).GetField("_capturerHero", BindingFlags.Instance | BindingFlags.NonPublic);


        [ProtoMember(1)]
        public string SettlementId { get; }
        [ProtoMember(2)]
        public string CapturerHeroId { get; }
        [ProtoMember(3)]
        public string ClanToExcludeId { get; }

        public SettlementClaimantDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string settlementId, string capturerHeroId, string clanToExcludeId) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            SettlementId = settlementId;
            CapturerHeroId = capturerHeroId;
            ClanToExcludeId = clanToExcludeId;
        }

        /// <inheritdoc/>
        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) || 
                !objectManager.TryGetObject(ClanToExcludeId, out Clan clanToExclude) ||
                !objectManager.TryGetObject(SettlementId, out Settlement settlement) ||
                !objectManager.TryGetObject(CapturerHeroId, out Hero capturerHero))
            {
                kingdomDecision = null;
                return false;
            }

            SettlementClaimantDecision settlementClaimantDecision = (SettlementClaimantDecision)FormatterServices.GetUninitializedObject(typeof(SettlementClaimantDecision));
            SetKingdomDecisionProperties(settlementClaimantDecision, proposerClan, kingdom);
            ClanToExcludeField.SetValue(settlementClaimantDecision, clanToExclude);
            SettlementField.SetValue(settlementClaimantDecision, settlement);
            CapturerHeroField.SetValue(settlementClaimantDecision, capturerHero);
            kingdomDecision = settlementClaimantDecision;
            return true;
        }
    }
}
