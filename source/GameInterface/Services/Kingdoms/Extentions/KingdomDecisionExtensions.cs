using GameInterface.Services.Kingdoms.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Extentions
{
    /// <summary>
    /// Class for extension methods for KingdomDecision class.
    /// </summary>
    public static class KingdomDecisionExtensions
    {

        private static readonly Dictionary<Type, Func<KingdomDecision, KingdomDecisionData>> SupportedConversions = new Dictionary<Type, Func<KingdomDecision, KingdomDecisionData>>()
        {
            { typeof(DeclareWarDecision), ConvertDeclareWarDecision },
            { typeof(ExpelClanFromKingdomDecision), ConvertExpelClanFromKingdomDecision },
            { typeof(KingdomPolicyDecision), ConvertKingdomPolicyDecision },
            { typeof(KingSelectionKingdomDecision), ConvertKingSelectionKingdomDecision },
            { typeof(MakePeaceKingdomDecision), ConvertMakePeaceKingdomDecision },
            { typeof(SettlementClaimantDecision), ConvertSettlementClaimantDecision },
            { typeof(SettlementClaimantPreliminaryDecision), ConvertSettlementClaimantPreliminaryDecision },
        };
        
        /// <summary>
        /// Converts a KingdomDecision object to a serializable KingdomDecisionData object.
        /// </summary>
        /// <param name="kingdomDecision">kingdom decision to convert.</param>
        /// <returns>A KingdomDecisionData object.</returns>
        /// <exception cref="InvalidOperationException">If KingdomDecision object is not convertable.</exception>
        public static KingdomDecisionData ToKingdomDecisionData(this KingdomDecision kingdomDecision)
        {
            if (SupportedConversions.ContainsKey(kingdomDecision.GetType()))
            {
                return SupportedConversions[kingdomDecision.GetType()](kingdomDecision);
            }
            else
            {
                throw new InvalidOperationException($"Type of kingdom decision: {kingdomDecision.GetType().Name} is not supported.");
            }
        }

        private static KingdomDecisionData ConvertDeclareWarDecision(KingdomDecision decision)
        {
            DeclareWarDecision declareWarDecision = decision as DeclareWarDecision;
            if (declareWarDecision != null)
            {
                return new DeclareWarDecisionData(declareWarDecision.ProposerClan.StringId, declareWarDecision.Kingdom.StringId,
                    declareWarDecision.TriggerTime._numTicks, declareWarDecision.IsEnforced, declareWarDecision.NotifyPlayer, declareWarDecision.PlayerExamined, declareWarDecision.FactionToDeclareWarOn.StringId);
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of DeclareWarDecision.");
            }
        }

        private static KingdomDecisionData ConvertKingdomPolicyDecision(KingdomDecision decision)
        {
            KingdomPolicyDecision kingdomPolicyDecision = decision as KingdomPolicyDecision;
            if (kingdomPolicyDecision != null)
            {
                return new KingdomPolicyDecisionData(kingdomPolicyDecision.ProposerClan.StringId, kingdomPolicyDecision.Kingdom.StringId,
                    kingdomPolicyDecision.TriggerTime._numTicks, kingdomPolicyDecision.IsEnforced, kingdomPolicyDecision.NotifyPlayer, kingdomPolicyDecision.PlayerExamined, kingdomPolicyDecision.Policy.StringId, kingdomPolicyDecision._isInvertedDecision, kingdomPolicyDecision._kingdomPolicies.Select(policy => policy.StringId).ToList());
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of KingdomPolicyDecision.");
            }
        }

        private static KingdomDecisionData ConvertExpelClanFromKingdomDecision(KingdomDecision decision)
        {
            ExpelClanFromKingdomDecision expelClanFromKingdomDecision = decision as ExpelClanFromKingdomDecision;
            if (expelClanFromKingdomDecision != null)
            {
                return new ExpelClanFromKingdomDecisionData(expelClanFromKingdomDecision.ProposerClan.StringId, expelClanFromKingdomDecision.Kingdom.StringId,
                    expelClanFromKingdomDecision.TriggerTime._numTicks, expelClanFromKingdomDecision.IsEnforced, expelClanFromKingdomDecision.NotifyPlayer, expelClanFromKingdomDecision.PlayerExamined, expelClanFromKingdomDecision.ClanToExpel.StringId, expelClanFromKingdomDecision.OldKingdom.StringId);
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of ExpelClanFromKingdomDecision.");
            }
        }

        private static KingdomDecisionData ConvertKingSelectionKingdomDecision(KingdomDecision decision)
        {
            KingSelectionKingdomDecision kingSelectionKingdomDecision = decision as KingSelectionKingdomDecision;
            if (kingSelectionKingdomDecision != null)
            {
                return new KingSelectionKingdomDecisionData(kingSelectionKingdomDecision.ProposerClan.StringId, kingSelectionKingdomDecision.Kingdom.StringId,
                    kingSelectionKingdomDecision.TriggerTime._numTicks, kingSelectionKingdomDecision.IsEnforced, kingSelectionKingdomDecision.NotifyPlayer, kingSelectionKingdomDecision.PlayerExamined, kingSelectionKingdomDecision._clanToExclude.StringId);
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of KingSelectionKingdomDecision.");
            }
        }

        private static KingdomDecisionData ConvertMakePeaceKingdomDecision(KingdomDecision decision)
        {
            MakePeaceKingdomDecision makePeaceKingdomDecision = decision as MakePeaceKingdomDecision;
            if (makePeaceKingdomDecision != null)
            {
                return new MakePeaceKingdomDecisionData(makePeaceKingdomDecision.ProposerClan.StringId, makePeaceKingdomDecision.Kingdom.StringId,
                    makePeaceKingdomDecision.TriggerTime._numTicks, makePeaceKingdomDecision.IsEnforced, makePeaceKingdomDecision.NotifyPlayer, makePeaceKingdomDecision.PlayerExamined, makePeaceKingdomDecision.FactionToMakePeaceWith.StringId, makePeaceKingdomDecision.DailyTributeToBePaid, makePeaceKingdomDecision._applyResults);
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of MakePeaceKingdomDecision.");
            }
        }

        private static KingdomDecisionData ConvertSettlementClaimantDecision(KingdomDecision decision)
        {
            SettlementClaimantDecision settlementClaimantDecision = decision as SettlementClaimantDecision;
            if (settlementClaimantDecision != null)
            {
                return new SettlementClaimantDecisionData(settlementClaimantDecision.ProposerClan.StringId, settlementClaimantDecision.Kingdom.StringId,
                    settlementClaimantDecision.TriggerTime._numTicks, settlementClaimantDecision.IsEnforced, settlementClaimantDecision.NotifyPlayer, settlementClaimantDecision.PlayerExamined, settlementClaimantDecision.Settlement.StringId, settlementClaimantDecision._capturerHero.StringId, settlementClaimantDecision.ClanToExclude.StringId);
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of SettlementClaimantDecision.");
            }
        }

        private static KingdomDecisionData ConvertSettlementClaimantPreliminaryDecision(KingdomDecision decision)
        {
            SettlementClaimantPreliminaryDecision settlementClaimantPreliminaryDecision = decision as SettlementClaimantPreliminaryDecision;
            if (settlementClaimantPreliminaryDecision != null)
            {
                return new SettlementClaimantPreliminaryDecisionData(settlementClaimantPreliminaryDecision.ProposerClan.StringId, settlementClaimantPreliminaryDecision.Kingdom.StringId,
                    settlementClaimantPreliminaryDecision.TriggerTime._numTicks, settlementClaimantPreliminaryDecision.IsEnforced, settlementClaimantPreliminaryDecision.NotifyPlayer, settlementClaimantPreliminaryDecision.PlayerExamined, settlementClaimantPreliminaryDecision.Settlement.StringId, settlementClaimantPreliminaryDecision._ownerClan.StringId);
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of SettlementClaimantPreliminaryDecision.");
            }
        }
    }
}
