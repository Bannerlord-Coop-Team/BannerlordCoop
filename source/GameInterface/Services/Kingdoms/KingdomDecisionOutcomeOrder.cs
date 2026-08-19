using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms
{
    public interface IKingdomDecisionOutcomeOrder
    {
        string[] CaptureKeys(IEnumerable<DecisionOutcome> outcomes, IObjectManager objectManager);
        IReadOnlyList<DecisionOutcome> ResolveOrderedOutcomes(
            IReadOnlyList<string> orderedKeys,
            IEnumerable<DecisionOutcome> localOutcomes,
            IEnumerable<DecisionOutcome> fullCandidates,
            IObjectManager objectManager);
    }

    internal class KingdomDecisionOutcomeOrder : IKingdomDecisionOutcomeOrder
    {
        private readonly IKingdomDecisionOutcomeResolver outcomeResolver;

        public KingdomDecisionOutcomeOrder(IKingdomDecisionOutcomeResolver outcomeResolver)
        {
            if (outcomeResolver == null) throw new ArgumentNullException(nameof(outcomeResolver));

            this.outcomeResolver = outcomeResolver;
        }

        public string[] CaptureKeys(IEnumerable<DecisionOutcome> outcomes, IObjectManager objectManager)
        {
            var keys = new List<string>();
            if (outcomes == null) return Array.Empty<string>();

            foreach (DecisionOutcome outcome in outcomes)
            {
                if (outcome == null) continue;
                if (!outcomeResolver.TryGetOutcomeKey(outcome, objectManager, out string outcomeKey)) continue;
                if (string.IsNullOrWhiteSpace(outcomeKey)) continue;

                keys.Add(outcomeKey);
            }

            return keys.ToArray();
        }

        public IReadOnlyList<DecisionOutcome> ResolveOrderedOutcomes(
            IReadOnlyList<string> orderedKeys,
            IEnumerable<DecisionOutcome> localOutcomes,
            IEnumerable<DecisionOutcome> fullCandidates,
            IObjectManager objectManager)
        {
            var ordered = new List<DecisionOutcome>();
            if (orderedKeys == null || orderedKeys.Count == 0) return ordered;

            Dictionary<string, DecisionOutcome> outcomesByKey = new Dictionary<string, DecisionOutcome>();
            AddOutcomes(outcomesByKey, localOutcomes, objectManager);
            AddOutcomes(outcomesByKey, fullCandidates, objectManager);

            foreach (string outcomeKey in orderedKeys)
            {
                if (string.IsNullOrWhiteSpace(outcomeKey)) return Array.Empty<DecisionOutcome>();
                if (!outcomesByKey.TryGetValue(outcomeKey, out DecisionOutcome outcome))
                {
                    return Array.Empty<DecisionOutcome>();
                }

                ordered.Add(outcome);
            }

            return ordered;
        }

        private void AddOutcomes(
            Dictionary<string, DecisionOutcome> outcomesByKey,
            IEnumerable<DecisionOutcome> outcomes,
            IObjectManager objectManager)
        {
            if (outcomes == null) return;

            foreach (DecisionOutcome outcome in outcomes)
            {
                if (outcome == null) continue;
                if (!outcomeResolver.TryGetOutcomeKey(outcome, objectManager, out string outcomeKey)) continue;
                if (string.IsNullOrWhiteSpace(outcomeKey)) continue;
                if (outcomesByKey.ContainsKey(outcomeKey)) continue;

                outcomesByKey.Add(outcomeKey, outcome);
            }
        }
    }
}
