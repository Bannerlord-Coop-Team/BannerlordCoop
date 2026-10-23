using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Election;
namespace GameInterface.Services.Kingdoms
{
    public interface IKingdomDecisionDataConverter : IGameAbstraction
    {
        /// <summary>
        /// True when <paramref name="kingdomDecision"/>'s exact type can be sent to clients. Other types are
        /// resolved on the server without a player vote, so they never enter the indexed decision list.
        /// </summary>
        bool IsSupported(KingdomDecision kingdomDecision);
        KingdomDecisionData Convert(KingdomDecision kingdomDecision);
        bool TryGetKingdomDecision(KingdomDecisionData data, out KingdomDecision kingdomDecision);

        /// <summary>
        /// Lets a compat mod sync its own decision type so players vote on it. Coop sends the shared decision
        /// fields (proposer, kingdom, trigger time, flags); <paramref name="write"/> and <paramref name="read"/>
        /// handle the rest. Register on the server and on every client, once per Coop session, through
        /// <c>ContainerProvider.TryResolve&lt;IKingdomDecisionDataConverter&gt;</c>.
        /// </summary>
        void Register<TDecision>(Func<TDecision, byte[]> write, Func<byte[], TDecision> read) where TDecision : KingdomDecision;
    }
    internal class KingdomDecisionDataConverter : IKingdomDecisionDataConverter
    {
        private readonly IObjectManager objectManager;
        private readonly Dictionary<Type, Func<KingdomDecision, KingdomDecisionData>> supportedConversions;
        private readonly Dictionary<string, Func<byte[], KingdomDecision>> registeredReaders = new Dictionary<string, Func<byte[], KingdomDecision>>(StringComparer.Ordinal);
        public KingdomDecisionDataConverter(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
            supportedConversions = new Dictionary<Type, Func<KingdomDecision, KingdomDecisionData>>()
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
        }
        public KingdomDecisionData Convert(KingdomDecision kingdomDecision)
        {
            if (!supportedConversions.TryGetValue(kingdomDecision.GetType(), out var conversionFunction))
            {
                throw new InvalidOperationException($"Type of kingdom decision: {kingdomDecision.GetType().Name} is not supported.");
            }
            return conversionFunction(kingdomDecision);
        }
        public bool IsSupported(KingdomDecision kingdomDecision)
        {
            return kingdomDecision != null && supportedConversions.ContainsKey(kingdomDecision.GetType());
        }
        public bool TryGetKingdomDecision(KingdomDecisionData data, out KingdomDecision kingdomDecision)
        {
            if (data is not RegisteredKingdomDecisionData registeredData)
            {
                return data.TryGetKingdomDecision(objectManager, out kingdomDecision);
            }

            if (registeredData.TypeName == null || !registeredReaders.TryGetValue(registeredData.TypeName, out var read))
            {
                kingdomDecision = null;
                return false;
            }

            return registeredData.TryGetKingdomDecision(objectManager, read, out kingdomDecision);
        }
        public void Register<TDecision>(Func<TDecision, byte[]> write, Func<byte[], TDecision> read) where TDecision : KingdomDecision
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            if (read == null) throw new ArgumentNullException(nameof(read));

            string typeName = typeof(TDecision).FullName;
            supportedConversions[typeof(TDecision)] = decision => new RegisteredKingdomDecisionData(GetId(decision.ProposerClan), GetId(decision.Kingdom),
                decision.TriggerTime._numTicks, decision.IsEnforced, decision.NotifyPlayer, decision.PlayerExamined, typeName, write((TDecision)decision));
            registeredReaders[typeName] = payload => read(payload);
        }
        private KingdomDecisionData ConvertDeclareWarDecision(KingdomDecision decision)
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
        private KingdomDecisionData ConvertKingdomPolicyDecision(KingdomDecision decision)
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
        private KingdomDecisionData ConvertExpelClanFromKingdomDecision(KingdomDecision decision)
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
        private KingdomDecisionData ConvertKingSelectionKingdomDecision(KingdomDecision decision)
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
        private KingdomDecisionData ConvertMakePeaceKingdomDecision(KingdomDecision decision)
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
        private KingdomDecisionData ConvertSettlementClaimantDecision(KingdomDecision decision)
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
        private KingdomDecisionData ConvertSettlementClaimantPreliminaryDecision(KingdomDecision decision)
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
        private KingdomDecisionData ConvertAcceptCallToWarAgreementDecision(KingdomDecision decision)
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
        private KingdomDecisionData ConvertProposeCallToWarAgreementDecision(KingdomDecision decision)
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
        private KingdomDecisionData ConvertStartAllianceDecision(KingdomDecision decision)
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
        private KingdomDecisionData ConvertTradeAgreementDecision(KingdomDecision decision)
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
        private string GetOptionalId(object obj)
        {
            return obj == null ? null : GetId(obj);
        }
        private string GetId(object obj)
        {
            if (obj == null) return null;
            if (objectManager.TryGetId(obj, out string id))
            {
                return id;
            }
            return null;
        }
    }
}
