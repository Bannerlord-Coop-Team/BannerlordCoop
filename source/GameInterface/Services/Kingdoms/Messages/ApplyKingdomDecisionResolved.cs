using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record ApplyKingdomDecisionResolved : ICommand
    {
        public string KingdomId { get; }
        public int DecisionIndex { get; }
        public int OutcomeIndex { get; }
        public string OutcomeKey { get; }
        public string NotificationText { get; }
        public bool IsPlayerDecision { get; }

        public ApplyKingdomDecisionResolved(string kingdomId, int decisionIndex, int outcomeIndex, bool isPlayerDecision)
            : this(kingdomId, decisionIndex, outcomeIndex, isPlayerDecision, null)
        {
        }

        public ApplyKingdomDecisionResolved(string kingdomId, int decisionIndex, int outcomeIndex, bool isPlayerDecision, string outcomeKey)
            : this(kingdomId, decisionIndex, outcomeIndex, isPlayerDecision, outcomeKey, null)
        {
        }

        public ApplyKingdomDecisionResolved(
            string kingdomId,
            int decisionIndex,
            int outcomeIndex,
            bool isPlayerDecision,
            string outcomeKey,
            string notificationText)
        {
            KingdomId = kingdomId;
            DecisionIndex = decisionIndex;
            OutcomeIndex = outcomeIndex;
            OutcomeKey = outcomeKey;
            NotificationText = notificationText;
            IsPlayerDecision = isPlayerDecision;
        }
    }
}
