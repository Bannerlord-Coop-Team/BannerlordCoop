using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class KingdomDecisionRoundStatusData
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public int DecisionIndex { get; }
        [ProtoMember(3)]
        public long DeadlineUtcTicks { get; }
        [ProtoMember(4)]
        public KingdomDecisionRoundClanStatusData[] Clans { get; }

        public KingdomDecisionRoundStatusData(
            string kingdomId,
            int decisionIndex,
            long deadlineUtcTicks,
            KingdomDecisionRoundClanStatusData[] clans)
        {
            KingdomId = kingdomId;
            DecisionIndex = decisionIndex;
            DeadlineUtcTicks = deadlineUtcTicks;
            Clans = clans;
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public class KingdomDecisionRoundClanStatusData
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string ClanName { get; }
        [ProtoMember(3)]
        public string PlayerNames { get; }
        [ProtoMember(4)]
        public bool HasFinalVote { get; }
        [ProtoMember(5)]
        public bool IsConnected { get; }

        public KingdomDecisionRoundClanStatusData(
            string clanId,
            string clanName,
            string playerNames,
            bool hasFinalVote,
            bool isConnected)
        {
            ClanId = clanId;
            ClanName = clanName;
            PlayerNames = playerNames;
            HasFinalVote = hasFinalVote;
            IsConnected = isConnected;
        }
    }
}
