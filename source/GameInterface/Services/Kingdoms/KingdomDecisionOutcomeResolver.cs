using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.ObjectManager;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms
{
    public interface IKingdomDecisionOutcomeResolver
    {
        bool TryGetOutcomeKey(DecisionOutcome outcome, IObjectManager objectManager, out string outcomeKey);
        bool TryGetOutcome(
            KingdomDecisionVoteData voteData,
            KingdomElection election,
            IObjectManager objectManager,
            out DecisionOutcome outcome);
    }

    internal class KingdomDecisionOutcomeResolver : IKingdomDecisionOutcomeResolver
    {
        private static readonly string[] ObjectOutcomeFieldNames =
        {
            "Clan",
            "King",
        };

        public bool TryGetOutcomeKey(DecisionOutcome outcome, IObjectManager objectManager, out string outcomeKey)
        {
            outcomeKey = null;
            if (outcome == null) return false;

            Type outcomeType = outcome.GetType();
            if (TryGetBooleanOutcome(outcome, out string fieldName, out bool value))
            {
                outcomeKey = $"{outcomeType.FullName}:{fieldName}={value}";
                return true;
            }

            foreach (string objectFieldName in ObjectOutcomeFieldNames)
            {
                FieldInfo field = outcomeType.GetField(objectFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) continue;
                object fieldValue = field.GetValue(outcome);
                if (!TryGetObjectId(fieldValue, objectManager, out string objectId)) continue;

                outcomeKey = $"{outcomeType.FullName}:{objectFieldName}={objectId}";
                return true;
            }

            return false;
        }

        public bool TryGetOutcome(
            KingdomDecisionVoteData voteData,
            KingdomElection election,
            IObjectManager objectManager,
            out DecisionOutcome outcome)
        {
            outcome = null;
            if (voteData == null || election == null) return false;

            if (!string.IsNullOrWhiteSpace(voteData.OutcomeKey))
            {
                foreach (DecisionOutcome possibleOutcome in election._possibleOutcomes)
                {
                    if (!TryGetOutcomeKey(possibleOutcome, objectManager, out string outcomeKey)) continue;
                    if (!string.Equals(outcomeKey, voteData.OutcomeKey, StringComparison.Ordinal)) continue;

                    outcome = possibleOutcome;
                    return true;
                }
            }

            if (voteData.OutcomeIndex < 0 || voteData.OutcomeIndex >= election._possibleOutcomes.Count) return false;

            outcome = election._possibleOutcomes[voteData.OutcomeIndex];
            return true;
        }

        private static bool TryGetBooleanOutcome(DecisionOutcome outcome, out string fieldName, out bool value)
        {
            switch (outcome)
            {
                case DeclareWarDecision.DeclareWarDecisionOutcome declareWarOutcome:
                    fieldName = nameof(DeclareWarDecision.DeclareWarDecisionOutcome.ShouldWarBeDeclared);
                    value = declareWarOutcome.ShouldWarBeDeclared;
                    return true;
                case MakePeaceKingdomDecision.MakePeaceDecisionOutcome makePeaceOutcome:
                    fieldName = nameof(MakePeaceKingdomDecision.MakePeaceDecisionOutcome.ShouldPeaceBeDeclared);
                    value = makePeaceOutcome.ShouldPeaceBeDeclared;
                    return true;
                case ExpelClanFromKingdomDecision.ExpelClanDecisionOutcome expelClanOutcome:
                    fieldName = nameof(ExpelClanFromKingdomDecision.ExpelClanDecisionOutcome.ShouldBeExpelled);
                    value = expelClanOutcome.ShouldBeExpelled;
                    return true;
                case KingdomPolicyDecision.PolicyDecisionOutcome policyOutcome:
                    fieldName = nameof(KingdomPolicyDecision.PolicyDecisionOutcome.ShouldDecisionBeEnforced);
                    value = policyOutcome.ShouldDecisionBeEnforced;
                    return true;
                case SettlementClaimantPreliminaryDecision.SettlementClaimantPreliminaryOutcome settlementClaimantOutcome:
                    fieldName = nameof(SettlementClaimantPreliminaryDecision.SettlementClaimantPreliminaryOutcome.ShouldSettlementOwnerChange);
                    value = settlementClaimantOutcome.ShouldSettlementOwnerChange;
                    return true;
                case AcceptCallToWarAgreementDecision.AcceptCallToWarAgreementDecisionOutcome acceptCallToWarOutcome:
                    fieldName = nameof(AcceptCallToWarAgreementDecision.AcceptCallToWarAgreementDecisionOutcome.ShouldAcceptCallToWar);
                    value = acceptCallToWarOutcome.ShouldAcceptCallToWar;
                    return true;
                case ProposeCallToWarAgreementDecision.ProposeCallToWarAgreementDecisionOutcome proposeCallToWarOutcome:
                    fieldName = nameof(ProposeCallToWarAgreementDecision.ProposeCallToWarAgreementDecisionOutcome.ShouldCallToWar);
                    value = proposeCallToWarOutcome.ShouldCallToWar;
                    return true;
                case StartAllianceDecision.StartAllianceDecisionOutcome startAllianceOutcome:
                    fieldName = nameof(StartAllianceDecision.StartAllianceDecisionOutcome.ShouldAllianceBeStarted);
                    value = startAllianceOutcome.ShouldAllianceBeStarted;
                    return true;
                case TradeAgreementDecision.TradeAgreementDecisionOutcome tradeAgreementOutcome:
                    fieldName = nameof(TradeAgreementDecision.TradeAgreementDecisionOutcome.ShouldTradeAgreementStart);
                    value = tradeAgreementOutcome.ShouldTradeAgreementStart;
                    return true;
                default:
                    fieldName = null;
                    value = false;
                    return false;
            }
        }

        private static bool TryGetObjectId(object value, IObjectManager objectManager, out string objectId)
        {
            objectId = null;
            if (value == null) return false;

            return objectManager != null && objectManager.TryGetId(value, out objectId);
        }
    }
}
