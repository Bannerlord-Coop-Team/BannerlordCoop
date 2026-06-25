using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class KingdomDecisionVoteData
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public int DecisionIndex { get; }
        [ProtoMember(3)]
        public int OutcomeIndex { get; }
        [ProtoMember(4)]
        public int SupportWeight { get; }
        [ProtoMember(5)]
        public bool IsAbstain { get; }
        [ProtoMember(6)]
        public bool IsFinal { get; }
        [ProtoMember(7)]
        public string OutcomeKey { get; }

        public KingdomDecisionVoteData(string kingdomId, int decisionIndex, int outcomeIndex, int supportWeight, bool isAbstain)
            : this(kingdomId, decisionIndex, outcomeIndex, supportWeight, isAbstain, false)
        {
        }

        public KingdomDecisionVoteData(
            string kingdomId,
            int decisionIndex,
            int outcomeIndex,
            int supportWeight,
            bool isAbstain,
            bool isFinal)
            : this(kingdomId, decisionIndex, outcomeIndex, supportWeight, isAbstain, isFinal, null)
        {
        }

        public KingdomDecisionVoteData(
            string kingdomId,
            int decisionIndex,
            int outcomeIndex,
            int supportWeight,
            bool isAbstain,
            bool isFinal,
            string outcomeKey)
        {
            KingdomId = kingdomId;
            DecisionIndex = decisionIndex;
            OutcomeIndex = outcomeIndex;
            SupportWeight = supportWeight;
            IsAbstain = isAbstain;
            IsFinal = isFinal;
            OutcomeKey = outcomeKey;
        }
    }
}
