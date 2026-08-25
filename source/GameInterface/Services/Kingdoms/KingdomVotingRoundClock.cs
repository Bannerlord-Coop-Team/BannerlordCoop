using System;

namespace GameInterface.Services.Kingdoms
{
    public interface IKingdomVotingRoundClock
    {
        DateTime CreateDeadline(DateTime utcNow);

        /// <summary>
        /// Returns the deadline an expired round should be held to, or null when it should resolve now.
        /// <paramref name="anyVoterInBattle"/> is only evaluated while a hold is still possible.
        /// </summary>
        DateTime? TryExtendDeadline(
            DateTime utcNow,
            DateTime deadline,
            DateTime roundStartedUtc,
            Func<bool> anyVoterInBattle);
    }

    internal class KingdomVotingRoundClock : IKingdomVotingRoundClock
    {
        public DateTime CreateDeadline(DateTime utcNow)
        {
            return utcNow + KingdomDecisionVoteManager.VotingRoundDuration;
        }

        // A voter locked in a battle cannot open the kingdom screen, so an expired round is held one more
        // duration at a time. The hold is capped from the round start, otherwise an afk voter in a battle
        // would keep the decision queued forever.
        public DateTime? TryExtendDeadline(
            DateTime utcNow,
            DateTime deadline,
            DateTime roundStartedUtc,
            Func<bool> anyVoterInBattle)
        {
            // The cap is checked first because the battle lookup scans every connected player.
            DateTime holdLimitUtc = roundStartedUtc + KingdomDecisionVoteManager.VotingRoundBattleHoldMaximum;
            if (deadline >= holdLimitUtc) return null;
            if (!anyVoterInBattle()) return null;

            DateTime extendedUtc = utcNow + KingdomDecisionVoteManager.VotingRoundDuration;
            if (extendedUtc > holdLimitUtc) extendedUtc = holdLimitUtc;

            // A tick that ran late can clamp back to a deadline that has already passed, which would
            // hold the round without ever moving it forward.
            return extendedUtc > deadline && extendedUtc > utcNow ? extendedUtc : (DateTime?)null;
        }
    }
}
