using Common.Extensions;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class SettlementClaimantDecisionData : KingdomDecisionData
    {
        private static Action<SettlementClaimantDecision, Settlement> SetSettlement = typeof(SettlementClaimantDecision).GetField(nameof(SettlementClaimantDecision.Settlement), BindingFlags.Instance | BindingFlags.Public).BuildUntypedSetter<SettlementClaimantDecision, Settlement>();
        private static Action<SettlementClaimantDecision, Clan> SetClanToExclude = typeof(SettlementClaimantDecision).GetField(nameof(SettlementClaimantDecision.ClanToExclude), BindingFlags.Instance | BindingFlags.Public).BuildUntypedSetter<SettlementClaimantDecision, Clan>();
        private static Action<SettlementClaimantDecision, Hero> SetCapturerHero = typeof(SettlementClaimantDecision).GetField("_capturerHero", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedSetter<SettlementClaimantDecision, Hero>();


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
            SetClanToExclude(settlementClaimantDecision, clanToExclude);
            SetSettlement(settlementClaimantDecision, settlement);
            SetCapturerHero(settlementClaimantDecision, capturerHero);
            kingdomDecision = settlementClaimantDecision;
            return true;
        }
    }
}
