using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.ObjectManager;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Kingdoms
{
    internal static class KingdomDecisionOutcomeResolver
    {
        private static readonly string[] BooleanOutcomeFieldNames =
        {
            "ShouldWarBeDeclared",
            "ShouldPeaceBeDeclared",
            "ShouldBeExpelled",
            "<ShouldDecisionBeEnforced>k__BackingField",
            "ShouldSettlementOwnerChange",
            "ShouldAcceptCallToWar",
            "ShouldCallToWar",
            "ShouldAllianceBeStarted",
            "ShouldTradeAgreementStart",
        };

        private static readonly string[] ObjectOutcomeFieldNames =
        {
            "Clan",
            "King",
        };

        public static bool TryGetOutcomeKey(DecisionOutcome outcome, IObjectManager objectManager, out string outcomeKey)
        {
            outcomeKey = null;
            if (outcome == null) return false;

            Type outcomeType = outcome.GetType();
            foreach (string fieldName in BooleanOutcomeFieldNames)
            {
                FieldInfo field = outcomeType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field?.FieldType != typeof(bool)) continue;

                outcomeKey = $"{outcomeType.FullName}:{fieldName}={field.GetValue(outcome)}";
                return true;
            }

            foreach (string fieldName in ObjectOutcomeFieldNames)
            {
                FieldInfo field = outcomeType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) continue;
                object value = field.GetValue(outcome);
                if (!TryGetObjectId(value, objectManager, out string objectId)) continue;

                outcomeKey = $"{outcomeType.FullName}:{fieldName}={objectId}";
                return true;
            }

            return false;
        }

        public static bool TryGetOutcome(
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

        private static bool TryGetObjectId(object value, IObjectManager objectManager, out string objectId)
        {
            objectId = null;
            if (value == null) return false;

            if (objectManager != null && objectManager.TryGetId(value, out objectId)) return true;

            if (value is MBObjectBase mbObject && !string.IsNullOrWhiteSpace(mbObject.StringId))
            {
                objectId = mbObject.StringId;
                return true;
            }

            return false;
        }
    }
}
