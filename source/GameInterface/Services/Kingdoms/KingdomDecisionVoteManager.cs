using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Localization;

namespace GameInterface.Services.Kingdoms
{
    public interface IKingdomDecisionVoteManager
    {
        void Reset();
        void RegisterDecision(KingdomDecision decision);
        bool TryCreateVoteData(DecisionOptionVM decisionOption, out KingdomDecisionVoteData voteData, bool isFinal = false);
        bool TryCreateVoteData(DecisionItemBaseVM decisionItem, out KingdomDecisionVoteData voteData, bool isFinal = false);
        bool TryPublishVote(DecisionOptionVM decisionOption);
        bool TryPublishFinalVote(DecisionItemBaseVM decisionItem);
        void MarkLocalVoteSubmitted(DecisionItemBaseVM decisionItem);
        bool ShouldSuppressLocalDecision(KingdomDecision decision);
        bool ShouldDisableResolveDecision(KingdomDecision decision);
        bool HasLocalPlayerSubmittedVote(KingdomDecision decision);
        bool ShouldBlockLocalResolution(DecisionItemBaseVM decisionItem);
        void RegisterDecisionItem(DecisionItemBaseVM decisionItem);
        void UnregisterDecisionItem(DecisionItemBaseVM decisionItem);
        bool HandleVoteRequest(string controllerId, KingdomDecisionVoteData voteData);
        void ApplyRemoteVote(string clanId, KingdomDecisionVoteData voteData);
        bool TryResolveDecision(KingdomDecision decision, bool force);
        bool HasEligiblePlayerClan(KingdomDecision decision);
        IReadOnlyList<KingdomDecisionVoteManager.KingdomDecisionDebugInfo> GetDecisionDebugInfo(Kingdom kingdom);
        void ApplyResolved(
            string kingdomId,
            int decisionIndex,
            int outcomeIndex,
            bool isPlayerDecision,
            string outcomeKey = null,
            string notificationText = null);
        void ClearDecisionState(string kingdomId, int decisionIndex);
    }

    public class KingdomDecisionVoteManager : IKingdomDecisionVoteManager
    {
        private static readonly ILogger Logger = LogManager.GetLogger(typeof(KingdomDecisionVoteManager));
        private readonly Dictionary<KingdomDecision, KingdomDecisionVoteState> DecisionStates = new Dictionary<KingdomDecision, KingdomDecisionVoteState>();
        private readonly HashSet<KingdomDecision> LocalSubmittedDecisions = new HashSet<KingdomDecision>();
        private readonly List<DecisionItemBaseVM> ActiveDecisionItems = new List<DecisionItemBaseVM>();
        private readonly List<PendingKingdomDecisionVote> PendingRemoteVotes = new List<PendingKingdomDecisionVote>();

        private readonly IPlayerManager playerManager;
        private readonly IObjectManager objectManager;
        private readonly IMessageBroker messageBroker;
        private readonly IKingdomDecisionOutcomeResolver outcomeResolver;

        public KingdomDecisionVoteManager(
            IPlayerManager playerManager,
            IObjectManager objectManager,
            IMessageBroker messageBroker,
            IKingdomDecisionOutcomeResolver outcomeResolver)
        {
            this.playerManager = playerManager;
            this.objectManager = objectManager;
            this.messageBroker = messageBroker;
            this.outcomeResolver = outcomeResolver;
        }

        public void Reset()
        {
            DecisionStates.Clear();
            LocalSubmittedDecisions.Clear();
            ActiveDecisionItems.Clear();
            PendingRemoteVotes.Clear();
        }

        public void RegisterDecision(KingdomDecision decision)
        {
            if (decision == null) return;
            KingdomDecisionVoteState state = GetOrCreateState(decision);
            ApplyPendingRemoteVotes(state);
        }

        public bool TryCreateVoteData(DecisionOptionVM decisionOption, out KingdomDecisionVoteData voteData, bool isFinal = false)
        {
            voteData = null;
            if (decisionOption == null || !IsLocalPlayerEligible(decisionOption.Decision)) return false;
            if (!TryGetDecisionIndex(decisionOption.Decision, out int decisionIndex)) return false;
            if (!TryGetKingdomId(decisionOption.Decision.Kingdom, out string kingdomId)) return false;

            int outcomeIndex = decisionOption.IsOptionForAbstain ? -1 : GetOutcomeIndex(decisionOption.Option, decisionOption._kingdomDecisionMaker);
            if (!decisionOption.IsOptionForAbstain && outcomeIndex < 0) return false;

            string outcomeKey = null;
            if (!decisionOption.IsOptionForAbstain)
            {
                outcomeResolver.TryGetOutcomeKey(decisionOption.Option, objectManager, out outcomeKey);
            }

            voteData = new KingdomDecisionVoteData(
                kingdomId,
                decisionIndex,
                outcomeIndex,
                GetSupportWeightValue(decisionOption.IsOptionForAbstain, decisionOption.CurrentSupportWeight),
                decisionOption.IsOptionForAbstain,
                isFinal,
                outcomeKey);
            return true;
        }

        public bool TryCreateVoteData(DecisionItemBaseVM decisionItem, out KingdomDecisionVoteData voteData, bool isFinal = false)
        {
            voteData = null;
            if (decisionItem?.KingdomDecisionMaker?._decision == null || decisionItem._currentSelectedOption == null) return false;

            KingdomDecision decision = decisionItem.KingdomDecisionMaker._decision;
            DecisionOptionVM selectedOption = decisionItem._currentSelectedOption;
            if (!IsLocalPlayerEligible(decision)) return false;
            if (!TryGetDecisionIndex(decision, out int decisionIndex)) return false;
            if (!TryGetKingdomId(decision.Kingdom, out string kingdomId)) return false;

            int outcomeIndex = selectedOption.IsOptionForAbstain
                ? -1
                : GetOutcomeIndex(selectedOption.Option, decisionItem.KingdomDecisionMaker);
            if (!selectedOption.IsOptionForAbstain && outcomeIndex < 0)
            {
                outcomeIndex = GetOutcomeIndex(selectedOption.Option, selectedOption._kingdomDecisionMaker);
            }
            if (!selectedOption.IsOptionForAbstain && outcomeIndex < 0) return false;

            string outcomeKey = null;
            if (!selectedOption.IsOptionForAbstain)
            {
                outcomeResolver.TryGetOutcomeKey(selectedOption.Option, objectManager, out outcomeKey);
            }

            voteData = new KingdomDecisionVoteData(
                kingdomId,
                decisionIndex,
                outcomeIndex,
                GetSupportWeightValue(selectedOption.IsOptionForAbstain, selectedOption.CurrentSupportWeight),
                selectedOption.IsOptionForAbstain,
                isFinal,
                outcomeKey);
            return true;
        }

        public bool TryPublishVote(DecisionOptionVM decisionOption)
        {
            if (!TryCreateVoteData(decisionOption, out KingdomDecisionVoteData voteData)) return false;

            TryApplyLocalVote(decisionOption.Decision, voteData);
            MessageBroker.Instance.Publish(decisionOption, new KingdomDecisionVoteRequested(voteData));
            return true;
        }

        public bool TryPublishFinalVote(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem == null || decisionItem._currentSelectedOption == null) return false;
            if (HasLocalPlayerSubmittedVote(decisionItem.KingdomDecisionMaker?._decision))
            {
                MarkLocalVoteSubmitted(decisionItem);
                return true;
            }

            if (!TryCreateVoteData(decisionItem, out KingdomDecisionVoteData voteData, isFinal: true))
            {
                Logger.Warning("Unable to publish final kingdom decision vote from the local decision UI.");
                return false;
            }

            TryApplyLocalVote(decisionItem.KingdomDecisionMaker._decision, voteData);
            MessageBroker.Instance.Publish(decisionItem, new KingdomDecisionVoteRequested(voteData));
            if (decisionItem.KingdomDecisionMaker?._decision != null)
            {
                LocalSubmittedDecisions.Add(decisionItem.KingdomDecisionMaker._decision);
            }
            MarkLocalVoteSubmitted(decisionItem);
            return true;
        }

        public void MarkLocalVoteSubmitted(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem == null) return;

            decisionItem._finalSelectionDone = true;
            decisionItem.IsActive = false;
            decisionItem.RefreshCanEndDecision();
            decisionItem._onDecisionOver?.Invoke();
            UnregisterDecisionItem(decisionItem);
        }

        public bool ShouldSuppressLocalDecision(KingdomDecision decision)
        {
            if (decision == null || Clan.PlayerClan == null) return false;
            if (Clan.PlayerClan.Kingdom != decision.Kingdom) return false;
            if (HasLocalPlayerSubmittedVote(decision)) return true;

            return !IsLocalPlayerEligible(decision);
        }

        public bool ShouldDisableResolveDecision(KingdomDecision decision)
        {
            return HasLocalPlayerSubmittedVote(decision);
        }

        public bool HasLocalPlayerSubmittedVote(KingdomDecision decision)
        {
            if (decision == null || Clan.PlayerClan == null) return false;
            if (Clan.PlayerClan.Kingdom != decision.Kingdom) return false;
            if (LocalSubmittedDecisions.Contains(decision)) return true;
            if (!TryGetClanId(Clan.PlayerClan, out string canonicalClanId)) return false;

            KingdomDecisionVoteState state = GetOrCreateState(decision);
            state.RefreshEligibleClanIds(GetEligibleClanIds(decision));
            ApplyPendingRemoteVotes(state);

            string clanId = Clan.PlayerClan.StringId;
            return GetCandidateClanIds(clanId, canonicalClanId)
                .Any(candidateClanId => state.FinalVotes.ContainsKey(candidateClanId));
        }

        public bool ShouldBlockLocalResolution(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem == null || decisionItem.KingdomDecisionMaker == null) return false;
            KingdomDecision decision = decisionItem.KingdomDecisionMaker._decision;
            if (decision == null || Clan.PlayerClan == null) return false;

            return IsLocalPlayerEligible(decision);
        }

        public void RegisterDecisionItem(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem == null || ActiveDecisionItems.Contains(decisionItem)) return;

            KingdomDecision decision = decisionItem.KingdomDecisionMaker?._decision;
            if (decision != null)
            {
                KingdomDecisionVoteState state = GetOrCreateState(decision);
                state.RefreshEligibleClanIds(GetEligibleClanIds(decision));
                ApplyPendingRemoteVotes(state);
            }

            ActiveDecisionItems.Add(decisionItem);
            ReplayVotes(decisionItem);
        }

        public void UnregisterDecisionItem(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem == null) return;
            ActiveDecisionItems.Remove(decisionItem);
        }

        public bool HandleVoteRequest(string controllerId, KingdomDecisionVoteData voteData)
        {
            if (string.IsNullOrEmpty(controllerId) || voteData == null) return false;
            voteData = NormalizeVoteData(voteData);
            if (!TryGetDecision(voteData, out KingdomDecision decision)) return false;
            if (!TryGetVoterClan(controllerId, decision, out Clan voterClan)) return false;
            if (!TryGetClanId(voterClan, out string voterClanId)) return false;

            KingdomDecisionVoteState state = GetOrCreateState(decision);
            state.RefreshEligibleClanIds(GetEligibleClanIds(decision));
            if (state.IsResolved || !state.EligibleClanIds.Contains(voterClanId)) return false;
            if (!ApplyVote(state, voterClanId, voterClan, voteData)) return false;

            messageBroker?.Publish(decision, new KingdomDecisionVoteChanged(voterClanId, voteData));

            if (state.HasAllVotes)
            {
                ResolveDecision(state);
            }
            return true;
        }

        public void ApplyRemoteVote(string clanId, KingdomDecisionVoteData voteData)
        {
            if (string.IsNullOrEmpty(clanId) || voteData == null) return;
            voteData = NormalizeVoteData(voteData);
            if (!TryGetDecision(voteData, out KingdomDecision decision) ||
                !TryGetClan(clanId, decision.Kingdom, out Clan clan))
            {
                QueuePendingRemoteVote(clanId, voteData);
                return;
            }

            KingdomDecisionVoteState state = GetOrCreateState(decision);
            state.RefreshEligibleClanIds(GetEligibleClanIds(decision));
            ApplyVote(state, clanId, clan, voteData);
        }

        public bool TryResolveDecision(KingdomDecision decision, bool force)
        {
            if (decision == null) return false;

            KingdomDecisionVoteState state = GetOrCreateState(decision);
            state.RefreshEligibleClanIds(GetEligibleClanIds(decision));
            if (state.EligibleClanIds.Count == 0) return false;
            if (!force && !state.HasAllVotes) return false;

            ResolveDecision(state);
            return true;
        }

        public bool HasEligiblePlayerClan(KingdomDecision decision)
        {
            return GetEligibleClanIds(decision).Count > 0;
        }

        public IReadOnlyList<KingdomDecisionDebugInfo> GetDecisionDebugInfo(Kingdom kingdom)
        {
            List<KingdomDecisionDebugInfo> decisionInfos = new List<KingdomDecisionDebugInfo>();
            if (kingdom?._unresolvedDecisions == null) return decisionInfos;

            foreach (KingdomDecision decision in kingdom._unresolvedDecisions.ToList())
            {
                if (decision == null) continue;
                KingdomDecisionVoteState state = GetOrCreateState(decision);
                state.RefreshEligibleClanIds(GetEligibleClanIds(decision));
                ApplyPendingRemoteVotes(state);

                decisionInfos.Add(CreateDecisionDebugInfo(state));
            }

            return decisionInfos;
        }

        public void ApplyResolved(
            string kingdomId,
            int decisionIndex,
            int outcomeIndex,
            bool isPlayerDecision,
            string outcomeKey = null,
            string notificationText = null)
        {
            if (!TryGetDecision(kingdomId, decisionIndex, out KingdomDecision decision))
            {
                PublishDecisionNotification(notificationText);
                return;
            }
            KingdomDecisionVoteState state = GetOrCreateState(decision);
            var voteData = new KingdomDecisionVoteData(
                kingdomId,
                decisionIndex,
                outcomeIndex,
                (int)Supporter.SupportWeights.StayNeutral,
                false,
                true,
                outcomeKey);
            if (!outcomeResolver.TryGetOutcome(voteData, state.Election, objectManager, out DecisionOutcome outcome))
            {
                PublishDecisionNotification(notificationText);
                return;
            }

            CampaignEventDispatcher.Instance.OnKingdomDecisionConcluded(decision, outcome, isPlayerDecision);
            PublishDecisionNotification(notificationText);
            ClearDecisionState(kingdomId, decisionIndex);
        }

        public void ClearDecisionState(string kingdomId, int decisionIndex)
        {
            if (string.IsNullOrWhiteSpace(kingdomId) || decisionIndex < 0) return;

            if (!TryGetDecision(kingdomId, decisionIndex, out KingdomDecision decision)) return;

            RemoveDecisionState(decision);
        }

        private void RemoveDecisionState(KingdomDecision decision)
        {
            if (decision == null) return;

            DecisionStates.Remove(decision);
            LocalSubmittedDecisions.Remove(decision);
            foreach (KingdomDecision staleDecision in DecisionStates.Keys
                         .Where(key => key == null || key.Kingdom == null)
                         .ToList())
            {
                DecisionStates.Remove(staleDecision);
            }
        }

        private bool ApplyVote(KingdomDecisionVoteState state, string clanId, Clan clan, KingdomDecisionVoteData voteData)
        {
            if (string.IsNullOrWhiteSpace(clanId)) return false;
            voteData = NormalizeVoteData(voteData);
            if (!TryGetSupportWeight(voteData.SupportWeight, out _)) return false;
            if (!voteData.IsFinal && state.FinalVotes.ContainsKey(clanId)) return false;

            if (!ApplyVoteToElection(state.Election, clan, voteData))
            {
                return false;
            }

            state.Votes[clanId] = new AppliedKingdomDecisionVote(clanId, voteData);
            if (voteData.IsFinal)
            {
                state.FinalVotes[clanId] = new AppliedKingdomDecisionVote(clanId, voteData);
            }
            ApplyVotesToActiveDecisionItems(state);
            return true;
        }

        private bool TryApplyLocalVote(KingdomDecision decision, KingdomDecisionVoteData voteData)
        {
            if (decision == null || Clan.PlayerClan == null) return false;
            if (!TryGetClanId(Clan.PlayerClan, out string clanId)) return false;
            KingdomDecisionVoteState state = GetOrCreateState(decision);
            state.RefreshEligibleClanIds(GetEligibleClanIds(decision));
            if (!state.EligibleClanIds.Contains(clanId)) return false;

            return ApplyVote(state, clanId, Clan.PlayerClan, voteData);
        }

        private void QueuePendingRemoteVote(string clanId, KingdomDecisionVoteData voteData)
        {
            if (string.IsNullOrEmpty(clanId) || voteData == null) return;
            voteData = NormalizeVoteData(voteData);
            if (string.IsNullOrWhiteSpace(voteData.KingdomId) || voteData.DecisionIndex < 0) return;

            PendingRemoteVotes.RemoveAll(vote =>
                vote.ClanId == clanId &&
                vote.VoteData.KingdomId == voteData.KingdomId &&
                vote.VoteData.DecisionIndex == voteData.DecisionIndex);
            PendingRemoteVotes.Add(new PendingKingdomDecisionVote(clanId, voteData));
        }

        private void ApplyPendingRemoteVotes(KingdomDecisionVoteState state)
        {
            foreach (PendingKingdomDecisionVote pendingVote in PendingRemoteVotes
                         .Where(vote => vote.VoteData.KingdomId == state.KingdomId &&
                         vote.VoteData.DecisionIndex == state.DecisionIndex)
                         .ToList())
            {
                if (!TryGetClan(pendingVote.ClanId, state.Decision.Kingdom, out Clan clan)) continue;

                if (ApplyVote(state, pendingVote.ClanId, clan, pendingVote.VoteData))
                {
                    PendingRemoteVotes.Remove(pendingVote);
                }
            }
        }

        private KingdomDecisionDebugInfo CreateDecisionDebugInfo(KingdomDecisionVoteState state)
        {
            List<KingdomDecisionClientVoteDebugInfo> clientVotes = new List<KingdomDecisionClientVoteDebugInfo>();
            if (playerManager != null)
            {
                foreach (Player player in playerManager.Players.OrderBy(player => player.ControllerId))
                {
                    clientVotes.Add(CreateClientVoteDebugInfo(state, player));
                }
            }

            return new KingdomDecisionDebugInfo(
                state.DecisionIndex,
                state.Decision.GetType().Name,
                clientVotes);
        }

        private KingdomDecisionClientVoteDebugInfo CreateClientVoteDebugInfo(KingdomDecisionVoteState state, Player player)
        {
            string clanId = player.ClanId;
            string clanName = "<none>";
            string canonicalClanId = null;
            bool isEligible = false;
            bool hasVote = false;
            bool isFinal = false;
            string status;
            string supportWeight = null;
            string outcome = null;

            if (string.IsNullOrEmpty(clanId))
            {
                status = "No Clan";
            }
            else if (!TryGetClan(clanId, state.Decision.Kingdom, out Clan clan))
            {
                status = "Clan Not Resolved";
            }
            else
            {
                clanName = clan.Name?.ToString() ?? clan.StringId;
                TryGetClanId(clan, out canonicalClanId);
                isEligible = clan.Kingdom == state.Decision.Kingdom &&
                    IsKnownEligibleClan(state, clanId, canonicalClanId);

                if (!isEligible)
                {
                    status = "Not Eligible";
                }
                else if (TryGetVoteForClan(state, clanId, canonicalClanId, out AppliedKingdomDecisionVote vote, out isFinal))
                {
                    hasVote = true;
                    status = GetVoteDebugStatus(state, vote.VoteData, isFinal, out outcome);
                    supportWeight = TryGetSupportWeight(vote.VoteData.SupportWeight, out Supporter.SupportWeights parsedSupportWeight)
                        ? parsedSupportWeight.ToString()
                        : vote.VoteData.SupportWeight.ToString();
                }
                else
                {
                    status = "Not Voted";
                }
            }

            return new KingdomDecisionClientVoteDebugInfo(
                player.ControllerId,
                clanId,
                clanName,
                status,
                supportWeight,
                outcome,
                isEligible,
                hasVote,
                isFinal);
        }

        private static bool IsKnownEligibleClan(KingdomDecisionVoteState state, string clanId, string canonicalClanId)
        {
            return GetCandidateClanIds(clanId, canonicalClanId)
                .Any(candidateClanId => state.EligibleClanIds.Contains(candidateClanId));
        }

        private static bool TryGetVoteForClan(
            KingdomDecisionVoteState state,
            string clanId,
            string canonicalClanId,
            out AppliedKingdomDecisionVote vote,
            out bool isFinal)
        {
            foreach (string candidateClanId in GetCandidateClanIds(clanId, canonicalClanId))
            {
                if (state.FinalVotes.TryGetValue(candidateClanId, out vote))
                {
                    isFinal = true;
                    return true;
                }
            }

            foreach (string candidateClanId in GetCandidateClanIds(clanId, canonicalClanId))
            {
                if (state.Votes.TryGetValue(candidateClanId, out vote))
                {
                    isFinal = false;
                    return true;
                }
            }

            vote = null;
            isFinal = false;
            return false;
        }

        private static IEnumerable<string> GetCandidateClanIds(string clanId, string canonicalClanId)
        {
            if (!string.IsNullOrWhiteSpace(clanId)) yield return clanId;
            if (!string.IsNullOrWhiteSpace(canonicalClanId) && canonicalClanId != clanId) yield return canonicalClanId;
        }

        private string GetVoteDebugStatus(
            KingdomDecisionVoteState state,
            KingdomDecisionVoteData voteData,
            bool isFinal,
            out string outcome)
        {
            if (voteData.IsAbstain)
            {
                outcome = "Abstain";
                return isFinal ? "Abstained" : "Selected Abstain";
            }

            outcome = GetOutcomeDebugText(state.Election, voteData);
            return isFinal ? $"Voted {outcome}" : $"Selected {outcome}";
        }

        private string GetOutcomeDebugText(KingdomElection election, KingdomDecisionVoteData voteData)
        {
            if (outcomeResolver.TryGetOutcome(voteData, election, objectManager, out DecisionOutcome outcome))
            {
                if (TryGetBooleanOutcomeText(outcome, out string booleanOutcomeText))
                {
                    return booleanOutcomeText;
                }

                string decisionTitle = outcome.GetDecisionTitle()?.ToString();
                if (!string.IsNullOrWhiteSpace(decisionTitle)) return decisionTitle;
            }

            if (TryGetBooleanOutcomeText(voteData.OutcomeKey, out string outcomeKeyText))
            {
                return outcomeKeyText;
            }

            return voteData.OutcomeIndex >= 0 ? $"Outcome {voteData.OutcomeIndex + 1}" : "Unknown";
        }

        private bool TryGetBooleanOutcomeText(DecisionOutcome outcome, out string outcomeText)
        {
            outcomeText = null;
            if (!outcomeResolver.TryGetOutcomeKey(outcome, objectManager, out string outcomeKey)) return false;

            return TryGetBooleanOutcomeText(outcomeKey, out outcomeText);
        }

        private static bool TryGetBooleanOutcomeText(string outcomeKey, out string outcomeText)
        {
            outcomeText = null;
            if (string.IsNullOrWhiteSpace(outcomeKey)) return false;

            if (outcomeKey.EndsWith("=True", StringComparison.OrdinalIgnoreCase))
            {
                outcomeText = "Yes";
                return true;
            }

            if (outcomeKey.EndsWith("=False", StringComparison.OrdinalIgnoreCase))
            {
                outcomeText = "No";
                return true;
            }

            return false;
        }

        private void ResolveDecision(KingdomDecisionVoteState state)
        {
            if (state.IsResolved) return;

            state.IsResolved = true;
            DecisionOutcome chosenOutcome = state.Election.ChooseOutcomeWithCurrentVotes();
            int outcomeIndex = GetOutcomeIndex(chosenOutcome, state.Election);
            outcomeResolver.TryGetOutcomeKey(chosenOutcome, objectManager, out string outcomeKey);
            KingdomDecision.SupportStatus supportStatus = GetSupportStatusOfDecisionOutcome(chosenOutcome);
            state.Decision.SupportStatusOfFinalDecision = supportStatus;
            string notificationText = GetDecisionNotificationText(state.Decision, chosenOutcome, supportStatus);

            messageBroker?.Publish(state.Decision, new KingdomDecisionResolved(
                state.KingdomId,
                state.DecisionIndex,
                outcomeIndex,
                true,
                outcomeKey,
                notificationText));

            if (!TryApplyDeclareWarOutcome(state.Decision, outcomeIndex))
            {
                state.Election.ApplyChosenOutcomeCoop();
            }
            if (state.Decision.Kingdom._unresolvedDecisions.Contains(state.Decision))
            {
                state.Decision.Kingdom.RemoveDecision(state.Decision);
            }
            RemoveDecisionState(state.Decision);
        }

        private string GetDecisionNotificationText(
            KingdomDecision decision,
            DecisionOutcome chosenOutcome,
            KingdomDecision.SupportStatus supportStatus)
        {
            if (decision == null || chosenOutcome == null) return null;

            try
            {
                string nativeText = decision.GetChosenOutcomeText(chosenOutcome, supportStatus, true)?.ToString();
                if (!string.IsNullOrWhiteSpace(nativeText)) return nativeText;
            }
            catch (Exception e)
            {
                Logger.Warning(e, "Failed to create kingdom decision notification text.");
            }

            return GetFallbackDecisionNotificationText(decision, chosenOutcome);
        }

        private string GetFallbackDecisionNotificationText(KingdomDecision decision, DecisionOutcome chosenOutcome)
        {
            if (decision is DeclareWarDecision declareWarDecision &&
                TryGetBooleanOutcome(chosenOutcome, "ShouldWarBeDeclared", out bool shouldWarBeDeclared))
            {
                string kingdomName = GetFactionName(declareWarDecision.Kingdom, "The kingdom");
                string targetName = GetFactionName(declareWarDecision.FactionToDeclareWarOn, "the target kingdom");

                return shouldWarBeDeclared
                    ? $"{kingdomName} has declared war on {targetName}."
                    : $"{kingdomName} chose not to go to war with {targetName}.";
            }

            string decisionTitle = GetText(decision.GetSupportTitle(), decision.GetType().Name);
            string outcomeTitle = GetText(chosenOutcome.GetDecisionTitle(), "the chosen outcome");
            return $"Kingdom decision resolved: {decisionTitle} - {outcomeTitle}.";
        }

        private static bool TryGetBooleanOutcome(DecisionOutcome outcome, string fieldName, out bool value)
        {
            value = false;
            FieldInfo fieldInfo = outcome?.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (fieldInfo?.FieldType != typeof(bool)) return false;

            value = (bool)fieldInfo.GetValue(outcome);
            return true;
        }

        private static string GetFactionName(IFaction faction, string fallback)
        {
            return GetText(faction?.InformalName ?? faction?.Name, fallback);
        }

        private static string GetText(TextObject textObject, string fallback)
        {
            try
            {
                string text = textObject?.ToString();
                return string.IsNullOrWhiteSpace(text) ? fallback : text;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static KingdomDecision.SupportStatus GetSupportStatusOfDecisionOutcome(DecisionOutcome outcome)
        {
            if (outcome == null) return KingdomDecision.SupportStatus.Equal;

            float supportPercentage = outcome.WinChance * 100f;
            if (supportPercentage > 55f) return KingdomDecision.SupportStatus.Majority;
            if (supportPercentage < 45f) return KingdomDecision.SupportStatus.Minority;
            return KingdomDecision.SupportStatus.Equal;
        }

        private void PublishDecisionNotification(string notificationText)
        {
            if (string.IsNullOrWhiteSpace(notificationText)) return;

            messageBroker?.Publish(typeof(KingdomDecisionVoteManager), new SendInformationMessage(notificationText));
        }

        private static bool TryApplyDeclareWarOutcome(KingdomDecision decision, int outcomeIndex)
        {
            if (outcomeIndex != 0) return false;
            if (decision is not DeclareWarDecision declareWarDecision) return false;
            if (declareWarDecision.Kingdom == null || declareWarDecision.FactionToDeclareWarOn == null) return false;
            if (FactionManager.IsAtWarAgainstFaction(declareWarDecision.Kingdom, declareWarDecision.FactionToDeclareWarOn)) return true;

            DeclareWarAction.ApplyByKingdomDecision(declareWarDecision.Kingdom, declareWarDecision.FactionToDeclareWarOn);
            return true;
        }

        private void ApplyVotesToActiveDecisionItems(KingdomDecisionVoteState state)
        {
            foreach (DecisionItemBaseVM decisionItem in ActiveDecisionItems.ToList())
            {
                if (decisionItem?.KingdomDecisionMaker?._decision == null) continue;
                if (decisionItem.KingdomDecisionMaker._decision != state.Decision) continue;

                ReplayVotes(decisionItem, state);
            }
        }

        private void ReplayVotes(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem?.KingdomDecisionMaker?._decision == null) return;
            if (!DecisionStates.TryGetValue(decisionItem.KingdomDecisionMaker._decision, out KingdomDecisionVoteState state)) return;

            ReplayVotes(decisionItem, state);
        }

        private void ReplayVotes(DecisionItemBaseVM decisionItem, KingdomDecisionVoteState state)
        {
            Dictionary<AppliedKingdomDecisionVote, Clan> votes = new Dictionary<AppliedKingdomDecisionVote, Clan>();

            foreach (AppliedKingdomDecisionVote vote in state.Votes.Values)
            {
                if (!TryGetClan(vote.ClanId, decisionItem.KingdomDecisionMaker._decision.Kingdom, out Clan clan)) continue;

                votes[vote] = clan;
            }

            List<Clan> clansToReset = votes.Values.ToList();
            foreach (string eligibleClanId in state.EligibleClanIds)
            {
                if (!TryGetClan(eligibleClanId, decisionItem.KingdomDecisionMaker._decision.Kingdom, out Clan clan)) continue;
                if (clansToReset.Contains(clan)) continue;

                clansToReset.Add(clan);
            }

            foreach (Clan clan in clansToReset)
            {
                ResetClanSupport(decisionItem.KingdomDecisionMaker, clan);
            }

            foreach (var vote in votes)
            {
                ApplyVoteToElection(decisionItem.KingdomDecisionMaker, vote.Value, vote.Key.VoteData, false);
            }
            RefreshDecisionItem(decisionItem);
        }

        private bool ApplyVoteToElection(KingdomElection election, Clan clan, KingdomDecisionVoteData voteData, bool resetExisting = true)
        {
            if (!TryGetSupportWeight(voteData.SupportWeight, out Supporter.SupportWeights supportWeight)) return false;

            Supporter supporter = new Supporter(clan);
            supporter.SupportWeight = supportWeight;
            if (resetExisting)
            {
                ResetClanSupport(election, clan);
            }

            if (voteData.IsAbstain)
            {
                if (election._chooser == clan)
                {
                    election._chosenOutcome = null;
                }
                election.DetermineOfficialSupport();
                return true;
            }

            if (!outcomeResolver.TryGetOutcome(voteData, election, objectManager, out DecisionOutcome selectedOutcome))
            {
                return false;
            }

            if (election._chooser == clan && election._decision.IsKingsVoteAllowed)
            {
                election._chosenOutcome = selectedOutcome;
            }

            selectedOutcome.AddSupport(supporter);
            election.DetermineOfficialSupport();
            return true;
        }

        private static void ResetClanSupport(KingdomElection election, Clan clan)
        {
            Supporter supporter = new Supporter(clan);
            foreach (DecisionOutcome outcome in election._possibleOutcomes)
            {
                outcome.ResetSupport(supporter);
            }
        }

        private void RefreshDecisionItem(DecisionItemBaseVM decisionItem)
        {
            decisionItem.RefreshWinPercentages();
            RefreshMultiplayerWinPercentages(decisionItem);
            decisionItem.RefreshInfluenceCost();
            decisionItem.RefreshCanEndDecision();
            foreach (DecisionOptionVM decisionOption in decisionItem.DecisionOptionsList)
            {
                RefreshDecisionOptionSupporters(decisionOption);
                decisionOption.RefreshValues();
            }
        }

        private static void RefreshMultiplayerWinPercentages(DecisionItemBaseVM decisionItem)
        {
            var decisionOptions = decisionItem.DecisionOptionsList
                .Where(option => !option.IsOptionForAbstain && option.Option != null)
                .ToList();
            Dictionary<DecisionOptionVM, float> optionSupportPoints = new Dictionary<DecisionOptionVM, float>();
            float totalSupportPoints = 0;

            foreach (DecisionOptionVM decisionOption in decisionOptions)
            {
                float supportPoints = 0;
                foreach (Supporter supporter in decisionOption.Option.SupporterList)
                {
                    supportPoints += Math.Max(0, (int)supporter.SupportWeight - 1);
                }

                optionSupportPoints[decisionOption] = supportPoints;
                totalSupportPoints += supportPoints;
            }

            if (totalSupportPoints <= 0) return;

            int assignedPercentage = 0;
            DecisionOptionVM remainderOption = null;
            float highestSupportPoints = -1;
            foreach (DecisionOptionVM decisionOption in decisionOptions)
            {
                int percentage = (int)Math.Floor(optionSupportPoints[decisionOption] / totalSupportPoints * 100);
                decisionOption.WinPercentage = percentage;
                assignedPercentage += percentage;

                if (optionSupportPoints[decisionOption] <= highestSupportPoints) continue;

                remainderOption = decisionOption;
                highestSupportPoints = optionSupportPoints[decisionOption];
            }

            if (remainderOption != null)
            {
                remainderOption.WinPercentage += 100 - assignedPercentage;
            }
        }

        private static void RefreshDecisionOptionSupporters(DecisionOptionVM decisionOption)
        {
            if (decisionOption?.SupportersOfThisOption == null) return;

            decisionOption.SupportersOfThisOption.Clear();
            decisionOption.SponsorWeightImagePath = null;
            if (decisionOption.Option?.SupporterList == null) return;

            foreach (Supporter supporter in decisionOption.Option.SupporterList)
            {
                if (supporter.SupportWeight <= Supporter.SupportWeights.StayNeutral) continue;

                if (supporter.Clan == decisionOption.Option.SponsorClan)
                {
                    decisionOption.SponsorWeightImagePath =
                        DecisionSupporterVM.GetSupporterWeightImagePath(supporter.SupportWeight);
                }

                decisionOption.SupportersOfThisOption.Add(CreateDecisionSupporter(supporter));
            }
        }

        private static DecisionSupporterVM CreateDecisionSupporter(Supporter supporter)
        {
            var supporterVm = new DecisionSupporterVM(
                supporter.Name,
                supporter.ImagePath,
                supporter.Clan,
                supporter.SupportWeight);

            TryApplyLeaderVisual(supporterVm, supporter.Clan);
            return supporterVm;
        }

        private static void TryApplyLeaderVisual(DecisionSupporterVM supporterVm, Clan clan)
        {
            var character = clan?.Leader?.CharacterObject;
            if (supporterVm == null || character == null) return;

            try
            {
                supporterVm.Visual = new CharacterImageIdentifierVM(
                    CampaignUIHelper.GetCharacterCode(character, false));
            }
            catch (Exception e)
            {
                Logger.Debug(
                    e,
                    "Unable to build kingdom decision supporter visual for clan {ClanId}.",
                    clan.StringId);
            }
        }

        private KingdomDecisionVoteState GetOrCreateState(KingdomDecision decision)
        {
            if (DecisionStates.TryGetValue(decision, out KingdomDecisionVoteState state))
            {
                TryGetKingdomId(decision.Kingdom, out string kingdomId);
                TryGetDecisionIndex(decision, out int decisionIndex);
                state.RefreshDecisionIdentity(kingdomId, decisionIndex);
                return state;
            }

            LocalSubmittedDecisions.Remove(decision);

            state = CreateState(decision);
            DecisionStates[decision] = state;
            return state;
        }

        private KingdomDecisionVoteState CreateState(KingdomDecision decision)
        {
            TryGetKingdomId(decision.Kingdom, out string kingdomId);
            TryGetDecisionIndex(decision, out int decisionIndex);
            return new KingdomDecisionVoteState(kingdomId, decisionIndex, decision, GetEligibleClanIds(decision));
        }

        private HashSet<string> GetEligibleClanIds(KingdomDecision decision)
        {
            HashSet<string> eligibleClanIds = new HashSet<string>();
            if (playerManager == null || objectManager == null) return eligibleClanIds;

            foreach (var player in playerManager.Players)
            {
                if (string.IsNullOrEmpty(player.ClanId)) continue;
                if (!TryGetClan(player.ClanId, decision.Kingdom, out Clan clan)) continue;
                if (clan.Kingdom != decision.Kingdom) continue;

                if (TryGetClanId(clan, out string clanId))
                {
                    eligibleClanIds.Add(clanId);
                }
            }
            return eligibleClanIds;
        }

        private bool TryGetVoterClan(string controllerId, KingdomDecision decision, out Clan clan)
        {
            clan = null;
            if (playerManager == null || objectManager == null) return false;
            if (!playerManager.TryGetPlayer(controllerId, out var player)) return false;
            if (!TryGetClan(player.ClanId, decision.Kingdom, out clan)) return false;
            if (clan.Kingdom != decision.Kingdom) return false;

            return true;
        }

        private bool TryGetClan(string clanId, Kingdom kingdom, out Clan clan)
        {
            clan = null;
            if (string.IsNullOrEmpty(clanId)) return false;
            if (objectManager != null && objectManager.TryGetObject(clanId, out clan)) return true;

            clan = kingdom?.Clans?.FirstOrDefault(existingClan =>
                existingClan != null &&
                (existingClan.StringId == clanId ||
                 (objectManager != null &&
                  objectManager.TryGetId(existingClan, out string existingClanId) &&
                  existingClanId == clanId)));

            return clan != null;
        }

        private bool TryGetClanId(Clan clan, out string clanId)
        {
            clanId = null;
            if (clan == null) return false;
            if (objectManager != null && objectManager.TryGetId(clan, out clanId)) return true;

            clanId = clan.StringId;
            return !string.IsNullOrWhiteSpace(clanId);
        }

        private static bool IsLocalPlayerEligible(KingdomDecision decision)
        {
            if (decision == null || Clan.PlayerClan == null) return false;
            if (Clan.PlayerClan.Kingdom != decision.Kingdom) return false;

            return true;
        }

        private bool TryGetDecision(KingdomDecisionVoteData voteData, out KingdomDecision decision)
        {
            decision = null;
            return voteData != null && TryGetDecision(voteData.KingdomId, voteData.DecisionIndex, out decision);
        }

        private bool TryGetDecision(string kingdomId, int decisionIndex, out KingdomDecision decision)
        {
            decision = null;
            if (objectManager == null) return false;
            if (!objectManager.TryGetObject(kingdomId, out Kingdom kingdom)) return false;
            if (kingdom._unresolvedDecisions == null) return false;
            if (decisionIndex < 0 || decisionIndex >= kingdom._unresolvedDecisions.Count) return false;

            decision = kingdom._unresolvedDecisions[decisionIndex];
            return true;
        }

        private static bool TryGetDecisionIndex(KingdomDecision decision, out int decisionIndex)
        {
            decisionIndex = -1;
            if (decision?.Kingdom?._unresolvedDecisions == null) return false;

            decisionIndex = decision.Kingdom._unresolvedDecisions.IndexOf(decision);
            return decisionIndex >= 0;
        }

        private bool TryGetKingdomId(Kingdom kingdom, out string kingdomId)
        {
            kingdomId = null;
            if (kingdom == null) return false;
            if (objectManager != null && objectManager.TryGetId(kingdom, out kingdomId)) return true;

            kingdomId = kingdom.StringId;
            return !string.IsNullOrWhiteSpace(kingdomId);
        }

        private static int GetOutcomeIndex(DecisionOutcome decisionOutcome, KingdomElection election)
        {
            if (decisionOutcome == null || election == null) return -1;

            for (int i = 0; i < election._possibleOutcomes.Count; i++)
            {
                if (election._possibleOutcomes[i] == decisionOutcome)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool TryGetSupportWeight(int supportWeightValue, out Supporter.SupportWeights supportWeight)
        {
            supportWeight = (Supporter.SupportWeights)supportWeightValue;
            return Enum.IsDefined(typeof(Supporter.SupportWeights), supportWeight);
        }

        private static int GetSupportWeightValue(bool isAbstain, Supporter.SupportWeights supportWeight)
        {
            if (!isAbstain && supportWeight <= Supporter.SupportWeights.StayNeutral)
            {
                return (int)Supporter.SupportWeights.FullyPush;
            }

            return (int)supportWeight;
        }

        private static KingdomDecisionVoteData NormalizeVoteData(KingdomDecisionVoteData voteData)
        {
            if (voteData == null || voteData.IsAbstain) return voteData;
            if (!TryGetSupportWeight(voteData.SupportWeight, out Supporter.SupportWeights supportWeight)) return voteData;

            int normalizedSupportWeight = GetSupportWeightValue(voteData.IsAbstain, supportWeight);
            if (normalizedSupportWeight == voteData.SupportWeight) return voteData;

            return new KingdomDecisionVoteData(
                voteData.KingdomId,
                voteData.DecisionIndex,
                voteData.OutcomeIndex,
                normalizedSupportWeight,
                voteData.IsAbstain,
                voteData.IsFinal,
                voteData.OutcomeKey);
        }

        private class KingdomDecisionVoteState
        {
            public string KingdomId { get; private set; }
            public KingdomDecision Decision { get; }
            public CoopKingdomElection Election { get; }
            public int DecisionIndex { get; private set; }
            public HashSet<string> EligibleClanIds { get; }
            public Dictionary<string, AppliedKingdomDecisionVote> Votes { get; }
            public Dictionary<string, AppliedKingdomDecisionVote> FinalVotes { get; }
            public bool IsResolved { get; set; }

            public bool HasAllVotes => EligibleClanIds.Count > 0 && EligibleClanIds.All(clanId => FinalVotes.ContainsKey(clanId));

            public KingdomDecisionVoteState(
                string kingdomId,
                int decisionIndex,
                KingdomDecision decision,
                HashSet<string> eligibleClanIds)
            {
                KingdomId = kingdomId;
                Decision = decision;
                DecisionIndex = decisionIndex;
                Election = new CoopKingdomElection(decision);
                Election.SetupPlayerVoteElection();
                EligibleClanIds = eligibleClanIds;
                Votes = new Dictionary<string, AppliedKingdomDecisionVote>();
                FinalVotes = new Dictionary<string, AppliedKingdomDecisionVote>();
            }

            public void RefreshDecisionIdentity(string kingdomId, int decisionIndex)
            {
                KingdomId = kingdomId;
                DecisionIndex = decisionIndex;
            }

            public void RefreshEligibleClanIds(HashSet<string> eligibleClanIds)
            {
                EligibleClanIds.Clear();
                foreach (string clanId in eligibleClanIds)
                {
                    EligibleClanIds.Add(clanId);
                }
            }
        }

        private class AppliedKingdomDecisionVote
        {
            public string ClanId { get; }
            public KingdomDecisionVoteData VoteData { get; }

            public AppliedKingdomDecisionVote(string clanId, KingdomDecisionVoteData voteData)
            {
                ClanId = clanId;
                VoteData = voteData;
            }
        }

        private class PendingKingdomDecisionVote
        {
            public string ClanId { get; }
            public KingdomDecisionVoteData VoteData { get; }

            public PendingKingdomDecisionVote(string clanId, KingdomDecisionVoteData voteData)
            {
                ClanId = clanId;
                VoteData = voteData;
            }
        }

        public class KingdomDecisionDebugInfo
        {
            public int DecisionIndex { get; }
            public string DecisionType { get; }
            public IReadOnlyList<KingdomDecisionClientVoteDebugInfo> ClientVotes { get; }

            public KingdomDecisionDebugInfo(
                int decisionIndex,
                string decisionType,
                IReadOnlyList<KingdomDecisionClientVoteDebugInfo> clientVotes)
            {
                DecisionIndex = decisionIndex;
                DecisionType = decisionType;
                ClientVotes = clientVotes;
            }
        }

        public class KingdomDecisionClientVoteDebugInfo
        {
            public string ControllerId { get; }
            public string ClanId { get; }
            public string ClanName { get; }
            public string Status { get; }
            public string SupportWeight { get; }
            public string Outcome { get; }
            public bool IsEligible { get; }
            public bool HasVote { get; }
            public bool IsFinal { get; }

            public KingdomDecisionClientVoteDebugInfo(
                string controllerId,
                string clanId,
                string clanName,
                string status,
                string supportWeight,
                string outcome,
                bool isEligible,
                bool hasVote,
                bool isFinal)
            {
                ControllerId = controllerId;
                ClanId = clanId;
                ClanName = clanName;
                Status = status;
                SupportWeight = supportWeight;
                Outcome = outcome;
                IsEligible = isEligible;
                HasVote = hasVote;
                IsFinal = isFinal;
            }
        }
    }
}
