using GameInterface.Configuration;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.StanceLinks;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms
{
    /// <summary>Which rule held an AI peace proposal back, if any.</summary>
    public enum PeaceProposalBlock
    {
        None,
        WarTooYoung,
        DeclineCooldown,
        PendingMirroredOffer,
    }

    /// <summary>
    /// Decides whether an AI clan may propose peace for a war right now. Two configurable rules sit
    /// on top of vanilla and are off by default: a minimum duration for wars a player faction is in,
    /// and a cooldown after the target declined the last offer. The third rule always applies and
    /// only suppresses a duplicate of an offer that is still unanswered. The player's own
    /// propose-peace path is never gated by any of this.
    /// </summary>
    public interface IAiPeaceProposalGate
    {
        bool IsPeaceProposalBlocked(IFaction proposingFaction, IFaction targetFaction, out PeaceProposalBlock blockingRule);

        /// <summary>Starts the cooldown for a mirrored offer the target did not accept. An offer that
        /// expires unanswered counts the same as an explicit refusal.</summary>
        void RecordDeclinedOffer(KingdomDecision decision);

        /// <summary>Drops every recorded decline. The cooldowns describe one campaign's diplomacy and
        /// must not carry into the next one loaded in the same process.</summary>
        void ClearDeclineCooldowns();
    }

    public class AiPeaceProposalGate : IAiPeaceProposalGate
    {
        private readonly object declineLock = new object();
        private readonly Dictionary<string, double> declineDayByFactionPair = new Dictionary<string, double>();

        public bool IsPeaceProposalBlocked(IFaction proposingFaction, IFaction targetFaction, out PeaceProposalBlock blockingRule)
        {
            blockingRule = PeaceProposalBlock.None;
            if (proposingFaction == null || targetFaction == null) return false;

            if (IsWarWithinMinimumDuration(proposingFaction, targetFaction))
            {
                blockingRule = PeaceProposalBlock.WarTooYoung;
            }
            else if (IsWithinDeclineCooldown(proposingFaction, targetFaction))
            {
                blockingRule = PeaceProposalBlock.DeclineCooldown;
            }
            else if (HasPendingMirroredOffer(targetFaction, proposingFaction))
            {
                blockingRule = PeaceProposalBlock.PendingMirroredOffer;
            }

            return blockingRule != PeaceProposalBlock.None;
        }

        public void RecordDeclinedOffer(KingdomDecision decision)
        {
            int cooldownDays = ConfiguredCooldownDays;
            if (cooldownDays <= 0) return;
            if (decision is not MakePeaceKingdomDecision { _isProposedByOpponent: true } peaceDecision) return;
            if (peaceDecision.Kingdom == null || peaceDecision.FactionToMakePeaceWith == null) return;

            RecordDecline(
                StanceLinkHandler.GetStanceLinkKey(peaceDecision.Kingdom, peaceDecision.FactionToMakePeaceWith),
                CampaignTime.Now.ToDays,
                cooldownDays);
        }

        public void ClearDeclineCooldowns()
        {
            lock (declineLock)
            {
                declineDayByFactionPair.Clear();
            }
        }

        /// <summary>
        /// The war-duration rule with no campaign statics in it. A minimum of 0 keeps vanilla
        /// behaviour, and a war with no player faction in it is never held back.
        /// </summary>
        internal static bool IsWarTooYoung(double warAgeInDays, int minimumWarDurationDays, bool involvesPlayerFaction)
        {
            if (minimumWarDurationDays <= 0) return false;
            if (!involvesPlayerFaction) return false;

            return warAgeInDays < minimumWarDurationDays;
        }

        /// <summary>
        /// True when <paramref name="targetFaction"/> still holds an unanswered offer mirrored from
        /// <paramref name="proposingFaction"/>. The vanilla duplicate guard compares proposer map
        /// factions, which never matches a mirrored offer because its proposer is the target's own
        /// ruling clan, so the same offer would otherwise be re-authored every day.
        /// </summary>
        internal static bool HasPendingMirroredOffer(IFaction targetFaction, IFaction proposingFaction)
        {
            if (targetFaction is not Kingdom targetKingdom || targetKingdom._unresolvedDecisions == null) return false;

            return targetKingdom.UnresolvedDecisions
                .OfType<MakePeaceKingdomDecision>()
                .Any(existing => existing._isProposedByOpponent
                                 && existing.FactionToMakePeaceWith == proposingFaction);
        }

        /// <summary>
        /// Records a decline and drops the ones that have already run out. Pruning happens here so
        /// the query stays a pure read, otherwise every pair that ever declined stays in here.
        /// </summary>
        internal void RecordDecline(string factionPairKey, double nowInDays, int cooldownDays)
        {
            lock (declineLock)
            {
                PruneElapsedCooldowns(nowInDays, cooldownDays);
                declineDayByFactionPair[factionPairKey] = nowInDays;
            }
        }

        /// <summary>
        /// The key is the shared faction-pair key, so one decline holds the pair back in both
        /// directions: neither side re-offers until the cooldown runs out.
        /// </summary>
        internal bool IsWithinDeclineCooldown(string factionPairKey, double nowInDays, int cooldownDays)
        {
            if (cooldownDays <= 0) return false;

            lock (declineLock)
            {
                if (!declineDayByFactionPair.TryGetValue(factionPairKey, out double declinedOnDay)) return false;

                return nowInDays - declinedOnDay < cooldownDays;
            }
        }

        private static int ConfiguredCooldownDays => ModConfigProvider.ModOptions.PeaceDeclineCooldownDays;

        private void PruneElapsedCooldowns(double nowInDays, int cooldownDays)
        {
            var elapsed = declineDayByFactionPair
                .Where(entry => nowInDays - entry.Value >= cooldownDays)
                .Select(entry => entry.Key)
                .ToList();

            foreach (string factionPairKey in elapsed)
            {
                declineDayByFactionPair.Remove(factionPairKey);
            }
        }

        private static bool IsWarWithinMinimumDuration(IFaction proposingFaction, IFaction targetFaction)
        {
            int minimumWarDurationDays = ModConfigProvider.ModOptions.MinimumWarDurationDays;
            // Early out so the default-off gate never reads campaign state.
            if (minimumWarDurationDays <= 0) return false;

            return IsWarTooYoung(
                GetWarAgeInDays(proposingFaction, targetFaction),
                minimumWarDurationDays,
                proposingFaction.IsPlayerFaction() || targetFaction.IsPlayerFaction());
        }

        /// <summary>Days this war has run, or <see cref="double.MaxValue"/> when the two are not at war.</summary>
        private static double GetWarAgeInDays(IFaction faction1, IFaction faction2)
        {
            if (!faction1.IsAtWarWith(faction2)) return double.MaxValue;

            StanceLink stance = faction1.GetStanceWith(faction2);
            if (stance == null) return double.MaxValue;

            return CampaignTime.Now.ToDays - stance._warStartDate.ToDays;
        }

        private bool IsWithinDeclineCooldown(IFaction faction1, IFaction faction2)
        {
            int cooldownDays = ConfiguredCooldownDays;
            // Early out so the default-off gate never reads campaign state.
            if (cooldownDays <= 0) return false;

            return IsWithinDeclineCooldown(
                StanceLinkHandler.GetStanceLinkKey(faction1, faction2),
                CampaignTime.Now.ToDays,
                cooldownDays);
        }
    }
}
