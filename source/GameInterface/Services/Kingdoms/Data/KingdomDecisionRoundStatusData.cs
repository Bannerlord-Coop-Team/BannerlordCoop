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
        [ProtoMember(5)]
        public string[] OrderedOutcomeKeys { get; }
        [ProtoMember(6)]
        public long ServerUtcTicks { get; }

        public KingdomDecisionRoundStatusData(
            string kingdomId,
            int decisionIndex,
            long deadlineUtcTicks,
            KingdomDecisionRoundClanStatusData[] clans,
            string[] orderedOutcomeKeys = null,
            long serverUtcTicks = 0)
        {
            KingdomId = kingdomId;
            DecisionIndex = decisionIndex;
            DeadlineUtcTicks = deadlineUtcTicks;
            Clans = clans;
            OrderedOutcomeKeys = orderedOutcomeKeys ?? System.Array.Empty<string>();
            ServerUtcTicks = serverUtcTicks;
        }

        public System.DateTime GetLocalDeadlineUtc(System.DateTime localUtcNow)
        {
            long referenceTicks = ServerUtcTicks > 0 ? ServerUtcTicks : localUtcNow.Ticks;
            long remainingTicks = System.Math.Max(0, DeadlineUtcTicks - referenceTicks);
            return localUtcNow + System.TimeSpan.FromTicks(remainingTicks);
        }

        public bool HasSameContent(KingdomDecisionRoundStatusData other)
        {
            if (other == null) return false;
            if (KingdomId != other.KingdomId ||
                DecisionIndex != other.DecisionIndex ||
                DeadlineUtcTicks != other.DeadlineUtcTicks)
            {
                return false;
            }

            KingdomDecisionRoundClanStatusData[] leftClans = Clans ?? System.Array.Empty<KingdomDecisionRoundClanStatusData>();
            KingdomDecisionRoundClanStatusData[] rightClans = other.Clans ?? System.Array.Empty<KingdomDecisionRoundClanStatusData>();
            if (leftClans.Length != rightClans.Length) return false;
            for (int i = 0; i < leftClans.Length; i++)
            {
                if (leftClans[i] == null || !leftClans[i].HasSameContent(rightClans[i])) return false;
            }

            string[] leftKeys = OrderedOutcomeKeys ?? System.Array.Empty<string>();
            string[] rightKeys = other.OrderedOutcomeKeys ?? System.Array.Empty<string>();
            if (leftKeys.Length != rightKeys.Length) return false;
            for (int i = 0; i < leftKeys.Length; i++)
            {
                if (leftKeys[i] != rightKeys[i]) return false;
            }

            return true;
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public class KingdomDecisionRoundVoteData
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public KingdomDecisionVoteData VoteData { get; }

        public KingdomDecisionRoundVoteData(string clanId, KingdomDecisionVoteData voteData)
        {
            ClanId = clanId;
            VoteData = voteData;
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

        public bool HasSameContent(KingdomDecisionRoundClanStatusData other)
        {
            return other != null &&
                   ClanId == other.ClanId &&
                   ClanName == other.ClanName &&
                   PlayerNames == other.PlayerNames &&
                   HasFinalVote == other.HasFinalVote &&
                   IsConnected == other.IsConnected;
        }
    }
}
