using GameInterface.Services.Kingdoms.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using GameInterface;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.ObjectSystem;

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
            { typeof(AcceptCallToWarAgreementDecision), ConvertAcceptCallToWarAgreementDecision },
            { typeof(ProposeCallToWarAgreementDecision), ConvertProposeCallToWarAgreementDecision },
            { typeof(StartAllianceDecision), ConvertStartAllianceDecision },
            { typeof(TradeAgreementDecision), ConvertTradeAgreementDecision },
        };
        
        /// <summary>
        /// Converts a KingdomDecision object to a serializable KingdomDecisionData object.
        /// </summary>
        /// <param name="kingdomDecision">kingdom decision to convert.</param>
        /// <returns>A KingdomDecisionData object.</returns>
        /// <exception cref="InvalidOperationException">If KingdomDecision object is not convertable.</exception>
        public static KingdomDecisionData ToKingdomDecisionData(this KingdomDecision kingdomDecision)
        {
            if (!SupportedConversions.TryGetValue(kingdomDecision.GetType(), out var conversionFunction))
            {
                throw new InvalidOperationException($"Type of kingdom decision: {kingdomDecision.GetType().Name} is not supported.");
            }
            return conversionFunction(kingdomDecision);
        }

        private static KingdomDecisionData ConvertDeclareWarDecision(KingdomDecision decision)
        {
            DeclareWarDecision declareWarDecision = decision as DeclareWarDecision;
            if (declareWarDecision != null)
            {
                return new DeclareWarDecisionData(GetId(declareWarDecision.ProposerClan), GetId(declareWarDecision.Kingdom),
                    declareWarDecision.TriggerTime._numTicks, declareWarDecision.IsEnforced, declareWarDecision.NotifyPlayer, declareWarDecision.PlayerExamined, GetId(declareWarDecision.FactionToDeclareWarOn));
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
                return new KingdomPolicyDecisionData(GetId(kingdomPolicyDecision.ProposerClan), GetId(kingdomPolicyDecision.Kingdom),
                    kingdomPolicyDecision.TriggerTime._numTicks, kingdomPolicyDecision.IsEnforced, kingdomPolicyDecision.NotifyPlayer, kingdomPolicyDecision.PlayerExamined, GetId(kingdomPolicyDecision.Policy), kingdomPolicyDecision._isInvertedDecision, kingdomPolicyDecision._kingdomPolicies.Select(GetId).ToList());
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
                return new ExpelClanFromKingdomDecisionData(GetId(expelClanFromKingdomDecision.ProposerClan), GetId(expelClanFromKingdomDecision.Kingdom),
                    expelClanFromKingdomDecision.TriggerTime._numTicks, expelClanFromKingdomDecision.IsEnforced, expelClanFromKingdomDecision.NotifyPlayer, expelClanFromKingdomDecision.PlayerExamined, GetId(expelClanFromKingdomDecision.ClanToExpel), GetId(expelClanFromKingdomDecision.OldKingdom));
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
                return new KingSelectionKingdomDecisionData(GetId(kingSelectionKingdomDecision.ProposerClan), GetId(kingSelectionKingdomDecision.Kingdom),
                    kingSelectionKingdomDecision.TriggerTime._numTicks, kingSelectionKingdomDecision.IsEnforced, kingSelectionKingdomDecision.NotifyPlayer, kingSelectionKingdomDecision.PlayerExamined, GetOptionalId(kingSelectionKingdomDecision._clanToExclude));
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
                return new MakePeaceKingdomDecisionData(GetId(makePeaceKingdomDecision.ProposerClan), GetId(makePeaceKingdomDecision.Kingdom),
                    makePeaceKingdomDecision.TriggerTime._numTicks, makePeaceKingdomDecision.IsEnforced, makePeaceKingdomDecision.NotifyPlayer, makePeaceKingdomDecision.PlayerExamined, GetId(makePeaceKingdomDecision.FactionToMakePeaceWith), makePeaceKingdomDecision.DailyTributeToBePaid, makePeaceKingdomDecision._applyResults, makePeaceKingdomDecision.DailyTributeDurationInDays, makePeaceKingdomDecision._isProposedByOpponent);
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
                return new SettlementClaimantDecisionData(GetId(settlementClaimantDecision.ProposerClan), GetId(settlementClaimantDecision.Kingdom),
                    settlementClaimantDecision.TriggerTime._numTicks, settlementClaimantDecision.IsEnforced, settlementClaimantDecision.NotifyPlayer, settlementClaimantDecision.PlayerExamined, GetId(settlementClaimantDecision.Settlement), GetOptionalId(settlementClaimantDecision._capturerHero), GetOptionalId(settlementClaimantDecision.ClanToExclude));
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
                return new SettlementClaimantPreliminaryDecisionData(GetId(settlementClaimantPreliminaryDecision.ProposerClan), GetId(settlementClaimantPreliminaryDecision.Kingdom),
                    settlementClaimantPreliminaryDecision.TriggerTime._numTicks, settlementClaimantPreliminaryDecision.IsEnforced, settlementClaimantPreliminaryDecision.NotifyPlayer, settlementClaimantPreliminaryDecision.PlayerExamined, GetId(settlementClaimantPreliminaryDecision.Settlement), GetId(settlementClaimantPreliminaryDecision._ownerClan));
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of SettlementClaimantPreliminaryDecision.");
            }
        }

        private static KingdomDecisionData ConvertAcceptCallToWarAgreementDecision(KingdomDecision decision)
        {
            AcceptCallToWarAgreementDecision acceptCallToWarAgreementDecision = decision as AcceptCallToWarAgreementDecision;
            if (acceptCallToWarAgreementDecision != null)
            {
                return new AcceptCallToWarAgreementDecisionData(GetId(acceptCallToWarAgreementDecision.ProposerClan), GetId(acceptCallToWarAgreementDecision.Kingdom),
                    acceptCallToWarAgreementDecision.TriggerTime._numTicks, acceptCallToWarAgreementDecision.IsEnforced, acceptCallToWarAgreementDecision.NotifyPlayer, acceptCallToWarAgreementDecision.PlayerExamined, GetId(acceptCallToWarAgreementDecision.CallingKingdom), GetId(acceptCallToWarAgreementDecision.KingdomToCallToWarAgainst), acceptCallToWarAgreementDecision.CallToWarCost);
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of AcceptCallToWarAgreementDecision.");
            }
        }

        private static KingdomDecisionData ConvertProposeCallToWarAgreementDecision(KingdomDecision decision)
        {
            ProposeCallToWarAgreementDecision proposeCallToWarAgreementDecision = decision as ProposeCallToWarAgreementDecision;
            if (proposeCallToWarAgreementDecision != null)
            {
                return new ProposeCallToWarAgreementDecisionData(GetId(proposeCallToWarAgreementDecision.ProposerClan), GetId(proposeCallToWarAgreementDecision.Kingdom),
                    proposeCallToWarAgreementDecision.TriggerTime._numTicks, proposeCallToWarAgreementDecision.IsEnforced, proposeCallToWarAgreementDecision.NotifyPlayer, proposeCallToWarAgreementDecision.PlayerExamined, GetId(proposeCallToWarAgreementDecision.CalledKingdom), GetId(proposeCallToWarAgreementDecision.KingdomToCallToWarAgainst), proposeCallToWarAgreementDecision.CallToWarCost);
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of ProposeCallToWarAgreementDecision.");
            }
        }

        private static KingdomDecisionData ConvertStartAllianceDecision(KingdomDecision decision)
        {
            StartAllianceDecision startAllianceDecision = decision as StartAllianceDecision;
            if (startAllianceDecision != null)
            {
                return new StartAllianceDecisionData(GetId(startAllianceDecision.ProposerClan), GetId(startAllianceDecision.Kingdom),
                    startAllianceDecision.TriggerTime._numTicks, startAllianceDecision.IsEnforced, startAllianceDecision.NotifyPlayer, startAllianceDecision.PlayerExamined, GetId(startAllianceDecision.KingdomToStartAllianceWith));
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of StartAllianceDecision.");
            }
        }

        private static KingdomDecisionData ConvertTradeAgreementDecision(KingdomDecision decision)
        {
            TradeAgreementDecision tradeAgreementDecision = decision as TradeAgreementDecision;
            if (tradeAgreementDecision != null)
            {
                return new TradeAgreementDecisionData(GetId(tradeAgreementDecision.ProposerClan), GetId(tradeAgreementDecision.Kingdom),
                    tradeAgreementDecision.TriggerTime._numTicks, tradeAgreementDecision.IsEnforced, tradeAgreementDecision.NotifyPlayer, tradeAgreementDecision.PlayerExamined, GetId(tradeAgreementDecision.TargetKingdom));
            }
            else
            {
                throw new ArgumentException($"Argument is not a type of TradeAgreementDecision.");
            }
        }

        private static string GetOptionalId(object obj)
        {
            return obj == null ? null : GetId(obj);
        }

        private static string GetId(object obj)
        {
            if (obj == null) return null;

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) &&
                objectManager.TryGetId(obj, out string id))
            {
                return id;
            }

            if (obj is MBObjectBase mbObject)
            {
                return mbObject.StringId;
            }

            return null;
        }
    }
}
