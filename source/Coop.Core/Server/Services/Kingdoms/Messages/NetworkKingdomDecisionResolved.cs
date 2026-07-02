using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public class NetworkKingdomDecisionResolved : ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public int DecisionIndex { get; }
        [ProtoMember(3)]
        public int OutcomeIndex { get; }
        [ProtoMember(4)]
        public bool IsPlayerDecision { get; }
        [ProtoMember(5)]
        public string OutcomeKey { get; }
        [ProtoMember(6)]
        public string NotificationText { get; }

        public NetworkKingdomDecisionResolved(string kingdomId, int decisionIndex, int outcomeIndex, bool isPlayerDecision)
            : this(kingdomId, decisionIndex, outcomeIndex, isPlayerDecision, null)
        {
        }

        public NetworkKingdomDecisionResolved(string kingdomId, int decisionIndex, int outcomeIndex, bool isPlayerDecision, string outcomeKey)
            : this(kingdomId, decisionIndex, outcomeIndex, isPlayerDecision, outcomeKey, null)
        {
        }

        public NetworkKingdomDecisionResolved(
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
            IsPlayerDecision = isPlayerDecision;
            OutcomeKey = outcomeKey;
            NotificationText = notificationText;
        }
    }
}
