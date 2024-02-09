using Common.Extensions;
using Coop.Mod.Extentions;
using GameInterface.Services.Kingdoms.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Extentions
{
    /// <summary>
    /// Class for extension methods for KingdomDecision class.
    /// </summary>
    public static class KingdomDecisionExtensions
    {
        private static Func<KingdomPolicyDecision, bool> GetIsInvertedDecision = typeof(KingdomPolicyDecision).GetField("_isInvertedDecision", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<KingdomPolicyDecision, bool>();
        private static Func<KingdomPolicyDecision, List<PolicyObject>> GetKingdomPolicies = typeof(KingdomPolicyDecision).GetField("_kingdomPolicies", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<KingdomPolicyDecision, List<PolicyObject>>();
        private static Func<KingSelectionKingdomDecision, Clan> GetClanToExclude = typeof(KingSelectionKingdomDecision).GetField("_clanToExclude", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<KingSelectionKingdomDecision, Clan>();
        private static Func<MakePeaceKingdomDecision, bool> GetApplyResults = typeof(MakePeaceKingdomDecision).GetField("_applyResults", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<MakePeaceKingdomDecision, bool>();
        private static Func<SettlementClaimantDecision, Hero> GetCapturerHero = typeof(SettlementClaimantDecision).GetField("_capturerHero", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<SettlementClaimantDecision, Hero>();
        private static Func<SettlementClaimantPreliminaryDecision, Clan> GetOwnerClan = typeof(SettlementClaimantPreliminaryDecision).GetField("_ownerClan", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedGetter<SettlementClaimantPreliminaryDecision, Clan>();

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
                    declareWarDecision.TriggerTime.GetNumTicks(), declareWarDecision.IsEnforced, declareWarDecision.NotifyPlayer, declareWarDecision.PlayerExamined, declareWarDecision.FactionToDeclareWarOn.StringId);
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
                    kingdomPolicyDecision.TriggerTime.GetNumTicks(), kingdomPolicyDecision.IsEnforced, kingdomPolicyDecision.NotifyPlayer, kingdomPolicyDecision.PlayerExamined, kingdomPolicyDecision.Policy.StringId, GetIsInvertedDecision(kingdomPolicyDecision), GetKingdomPolicies(kingdomPolicyDecision).Select(policy => policy.StringId).ToList());
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
                    expelClanFromKingdomDecision.TriggerTime.GetNumTicks(), expelClanFromKingdomDecision.IsEnforced, expelClanFromKingdomDecision.NotifyPlayer, expelClanFromKingdomDecision.PlayerExamined, expelClanFromKingdomDecision.ClanToExpel.StringId, expelClanFromKingdomDecision.OldKingdom.StringId);
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
                    kingSelectionKingdomDecision.TriggerTime.GetNumTicks(), kingSelectionKingdomDecision.IsEnforced, kingSelectionKingdomDecision.NotifyPlayer, kingSelectionKingdomDecision.PlayerExamined, GetClanToExclude(kingSelectionKingdomDecision).StringId);
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
                    makePeaceKingdomDecision.TriggerTime.GetNumTicks(), makePeaceKingdomDecision.IsEnforced, makePeaceKingdomDecision.NotifyPlayer, makePeaceKingdomDecision.PlayerExamined, makePeaceKingdomDecision.FactionToMakePeaceWith.StringId, makePeaceKingdomDecision.DailyTributeToBePaid, GetApplyResults(makePeaceKingdomDecision));
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
                    settlementClaimantDecision.TriggerTime.GetNumTicks(), settlementClaimantDecision.IsEnforced, settlementClaimantDecision.NotifyPlayer, settlementClaimantDecision.PlayerExamined, settlementClaimantDecision.Settlement.StringId, GetCapturerHero(settlementClaimantDecision).StringId, settlementClaimantDecision.ClanToExclude.StringId);
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
                    settlementClaimantPreliminaryDecision.TriggerTime.GetNumTicks(), settlementClaimantPreliminaryDecision.IsEnforced, settlementClaimantPreliminaryDecision.NotifyPlayer, settlementClaimantPreliminaryDecision.PlayerExamined, settlementClaimantPreliminaryDecision.Settlement.StringId, GetOwnerClan(settlementClaimantPreliminaryDecision).StringId);
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of SettlementClaimantPreliminaryDecision.");
            }
        }
    }
}
