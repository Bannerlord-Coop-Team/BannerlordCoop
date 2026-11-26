using Common.Messaging;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Messages
{
    public record ApplyKingdomDecisionResolved : ICommand
    {
        public string KingdomId { get; }
        public int DecisionIndex { get; }
        public int OutcomeIndex { get; }
        public string OutcomeKey { get; }
        public string NotificationText { get; }
        public KingdomDecision.SupportStatus SupportStatus { get; }
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
            string notificationText,
            KingdomDecision.SupportStatus supportStatus = KingdomDecision.SupportStatus.Equal)
        {
            KingdomId = kingdomId;
            DecisionIndex = decisionIndex;
            OutcomeIndex = outcomeIndex;
            OutcomeKey = outcomeKey;
            NotificationText = notificationText;
            SupportStatus = supportStatus;
            IsPlayerDecision = isPlayerDecision;
        }
    }
}
