using Common;
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
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
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
        void CloseDecisionItem(DecisionItemBaseVM decisionItem);
        void ApplyRoundStatus(KingdomDecisionRoundStatusData status);
        IReadOnlyList<KingdomDecisionRoundStatusData> CaptureActiveRoundStatuses();
        string RefreshDecisionWaitingStatus(DecisionItemBaseVM decisionItem);
        IReadOnlyList<string> GetDecisionWaitingColumns(DecisionItemBaseVM decisionItem);
        string RefreshDecisionTitle(DecisionItemBaseVM decisionItem);
        bool ShouldSuppressLocalDecision(KingdomDecision decision);
        bool ShouldDisableResolveDecision(KingdomDecision decision);
        bool HasLocalPlayerSubmittedVote(KingdomDecision decision);
        bool ShouldBlockLocalResolution(DecisionItemBaseVM decisionItem);
        void RegisterDecisionItem(DecisionItemBaseVM decisionItem);
        void UnregisterDecisionItem(DecisionItemBaseVM decisionItem);
        bool HandleVoteRequest(string controllerId, KingdomDecisionVoteData voteData);
        void ApplyRemoteVote(string clanId, KingdomDecisionVoteData voteData);
        bool TryResolveDecision(KingdomDecision decision);
        bool HasEligiblePlayerClan(KingdomDecision decision);
        bool TryPublishFinalVoteForElection(KingdomElection election);
        IReadOnlyList<KingdomDecisionVoteManager.KingdomDecisionDebugInfo> GetDecisionDebugInfo(Kingdom kingdom);
        void ApplyResolved(
            string kingdomId,
            int decisionIndex,
            int outcomeIndex,
            bool isPlayerDecision,
            string outcomeKey = null,
            string notificationText = null);
        void CloseDecision(string kingdomId, int decisionIndex);
        void ClearDecisionState(string kingdomId, int decisionIndex);
        void ClearDecisionState(KingdomDecision decision);
    }

    public class KingdomDecisionVoteManager : IKingdomDecisionVoteManager, IDisposable
    {
        internal static readonly TimeSpan VotingRoundDuration = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan VotingRoundTickInterval = TimeSpan.FromSeconds(1);
        private static readonly ILogger Logger = LogManager.GetLogger(typeof(KingdomDecisionVoteManager));
        private readonly Dictionary<KingdomDecision, KingdomDecisionVoteState> DecisionStates = new Dictionary<KingdomDecision, KingdomDecisionVoteState>();
        private readonly HashSet<KingdomDecision> LocalSubmittedDecisions = new HashSet<KingdomDecision>();
        private readonly List<DecisionItemBaseVM> ActiveDecisionItems = new List<DecisionItemBaseVM>();
        private readonly List<PendingKingdomDecisionVote> PendingRemoteVotes = new List<PendingKingdomDecisionVote>();
        private readonly List<KingdomDecisionRoundStatusData> PendingRoundStatuses = new List<KingdomDecisionRoundStatusData>();

        private readonly IPlayerManager playerManager;
        private readonly IObjectManager objectManager;
        private readonly IMessageBroker messageBroker;
        private readonly IKingdomDecisionOutcomeResolver outcomeResolver;
        private readonly IKingdomDecisionOutcomeOrder outcomeOrder;
        private readonly IKingdomDecisionRoundPresentation roundPresentation;
        private readonly Timer votingRoundTimer;
        private int isDisposed;

        public KingdomDecisionVoteManager(
            IPlayerManager playerManager,
            IObjectManager objectManager,
            IMessageBroker messageBroker,
            IKingdomDecisionOutcomeResolver outcomeResolver,
            IKingdomDecisionOutcomeOrder outcomeOrder,
            IKingdomDecisionRoundPresentation roundPresentation)
        {
            this.playerManager = playerManager;
            this.objectManager = objectManager;
            this.messageBroker = messageBroker;
            this.outcomeResolver = outcomeResolver;
            this.outcomeOrder = outcomeOrder;
            this.roundPresentation = roundPresentation;

            if (ModInformation.IsServer)
            {
                votingRoundTimer = new Timer(_ => QueueVotingRoundTick(), null, VotingRoundTickInterval, VotingRoundTickInterval);
            }
        }

        public void Reset()
        {
            DecisionStates.Clear();
            LocalSubmittedDecisions.Clear();
            ActiveDecisionItems.Clear();
            PendingRemoteVotes.Clear();
            PendingRoundStatuses.Clear();
        }

        public void RegisterDecision(KingdomDecision decision)
        {
            if (decision == null) return;
            KingdomDecisionVoteState state = GetOrCreateState(decision);
            ApplyPendingRemoteVotes(state);
            ApplyPendingRoundStatus(state);
            if (ModInformation.IsServer)
            {
                PublishRoundStatus(state);
            }
        }

        public bool TryCreateVoteData(DecisionOptionVM decisionOption, out KingdomDecisionVoteData voteData, bool isFinal = false)
        {
            voteData = null;
            if (decisionOption == null || !IsLocalPlayerEligible(decisionOption.Decision)) return false;
            if (!TryGetDecisionIndex(decisionOption.Decision, out int decisionIndex)) return false;
            if (!TryGetKingdomId(decisionOption.Decision.Kingdom, out string kingdomId)) return false;

            int outcomeIndex = decisionOption.IsOptionForAbstain
                ? -1
                : GetOutcomeIndex(decisionOption.Option, decisionOption._kingdomDecisionMaker);
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
            if (HasLocalPlayerSubmittedVote(decisionOption?.Decision)) return false;
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
            ShowSubmittedState(decisionItem);
            return true;
        }

        public void ApplyRoundStatus(KingdomDecisionRoundStatusData status)
        {
            if (status == null) return;
            if (!TryGetDecision(status.KingdomId, status.DecisionIndex, out KingdomDecision decision))
            {
                PendingRoundStatuses.RemoveAll(candidate =>
                    candidate.KingdomId == status.KingdomId &&
                    candidate.DecisionIndex == status.DecisionIndex);
                PendingRoundStatuses.Add(status);
                return;
            }

            KingdomDecisionVoteState state = GetOrCreateState(decision);
            if (state.LastPublishedRoundStatus != null &&
                state.LastPublishedRoundStatus.HasSameContent(status))
            {
                return;
            }

            state.LastPublishedRoundStatus = status;
            state.ApplyRoundStatus(status);
            ApplyAuthoritativeOutcomes(state);
            foreach (DecisionItemBaseVM decisionItem in ActiveDecisionItems.ToList())
            {
                if (decisionItem?.KingdomDecisionMaker?._decision == decision)
                {
                    ApplyAuthoritativeOutcomes(state, decisionItem);
                    if (HasLocalPlayerSubmittedVote(decision))
                    {
                        ShowSubmittedState(decisionItem);
                    }
                    else
                    {
                        RefreshDecisionPresentation(decisionItem);
                    }
                }
            }
        }

        public IReadOnlyList<KingdomDecisionRoundStatusData> CaptureActiveRoundStatuses()
        {
            if (!ModInformation.IsServer) return Array.Empty<KingdomDecisionRoundStatusData>();

            RegisterLoadedDecisions();
            return DecisionStates.Values
                .Where(state => IsDecisionUnresolved(state.Decision) &&
                                !state.IsResolved &&
                                state.RoundDeadlineUtc.HasValue)
                .Select(CreateRoundStatus)
                .Where(status => status != null)
                .ToArray();
        }

        private void ApplyPendingRoundStatus(KingdomDecisionVoteState state)
        {
            KingdomDecisionRoundStatusData pending = PendingRoundStatuses.LastOrDefault(candidate =>
                candidate.KingdomId == state.KingdomId &&
                candidate.DecisionIndex == state.DecisionIndex);
            if (pending == null) return;

            PendingRoundStatuses.RemoveAll(candidate =>
                candidate.KingdomId == state.KingdomId &&
                candidate.DecisionIndex == state.DecisionIndex);
            state.ApplyRoundStatus(pending);
            ApplyAuthoritativeOutcomes(state);
        }

        public string RefreshDecisionWaitingStatus(DecisionItemBaseVM decisionItem)
        {
            return GetWaitingFeedback(decisionItem)?.Header;
        }

        public IReadOnlyList<string> GetDecisionWaitingColumns(DecisionItemBaseVM decisionItem)
        {
            return GetWaitingFeedback(decisionItem)?.Columns ?? Array.Empty<string>();
        }

        public string RefreshDecisionTitle(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem == null) return null;

            string baseTitle = GetBaseDecisionTitle(decisionItem);
            int? remainingSeconds = TryGetRemainingSeconds(decisionItem);
            string title = roundPresentation.FormatTitle(baseTitle, remainingSeconds);
            decisionItem.TitleText = title;
            return title;
        }

        public void CloseDecisionItem(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem == null) return;

            decisionItem._finalSelectionDone = true;
            decisionItem.IsActive = false;
            decisionItem.RefreshCanEndDecision();
            CampaignEvents.KingdomDecisionConcluded.ClearListeners(decisionItem);
            decisionItem._onDecisionOver?.Invoke();
            UnregisterDecisionItem(decisionItem);
        }

        public bool ShouldSuppressLocalDecision(KingdomDecision decision)
        {
            if (decision == null || Clan.PlayerClan == null) return false;
            if (Clan.PlayerClan.Kingdom != decision.Kingdom) return false;
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
            RefreshEligibleClanIds(state, decision);
            ApplyPendingRemoteVotes(state);

            if (state.FinalVotes.ContainsKey(canonicalClanId)) return true;

            return state.RoundClans.TryGetValue(canonicalClanId, out KingdomDecisionRoundClanStatusData roundClan) &&
                   roundClan.HasFinalVote;
        }

        public bool ShouldBlockLocalResolution(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem == null || decisionItem.KingdomDecisionMaker == null) return false;
            KingdomDecision decision = decisionItem.KingdomDecisionMaker._decision;
            if (decision == null || Clan.PlayerClan == null) return false;

            if (DecisionStates.TryGetValue(decision, out KingdomDecisionVoteState state) && state.HasRoundSnapshot)
            {
                return Clan.PlayerClan.Kingdom == decision.Kingdom;
            }

            return IsLocalPlayerEligible(decision);
        }

        public void RegisterDecisionItem(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem == null || ActiveDecisionItems.Contains(decisionItem)) return;

            KingdomDecision decision = decisionItem.KingdomDecisionMaker?._decision;
            if (decision != null)
            {
                KingdomDecisionVoteState state = GetOrCreateState(decision);
                RefreshEligibleClanIds(state, decision);
                ApplyPendingRemoteVotes(state);
            }

            ActiveDecisionItems.Add(decisionItem);
            if (decision != null && DecisionStates.TryGetValue(decision, out KingdomDecisionVoteState registeredState))
            {
                ApplyAuthoritativeOutcomes(registeredState, decisionItem);
            }
            ReplayVotes(decisionItem);
            if (HasLocalPlayerSubmittedVote(decision))
            {
                ShowSubmittedState(decisionItem);
            }
            else
            {
                RefreshDecisionPresentation(decisionItem);
            }
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
            RefreshEligibleClanIds(state, decision);
            if (state.IsResolved || TryResolveExpiredRound(state, DateTime.UtcNow)) return false;
            if (!state.EligibleClanIds.Contains(voterClanId)) return false;
            if (!ApplyVote(state, voterClanId, voterClan, voteData)) return false;

            messageBroker?.Publish(decision, new KingdomDecisionVoteChanged(voterClanId, voteData));
            PublishRoundStatus(state);

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
            RefreshEligibleClanIds(state, decision);
            ApplyVote(state, clanId, clan, voteData);
        }

        public bool TryResolveDecision(KingdomDecision decision)
        {
            if (!IsDecisionUnresolved(decision)) return false;

            KingdomDecisionVoteState state = GetOrCreateState(decision);
            RefreshEligibleClanIds(state, decision);
            if (state.EligibleClanIds.Count == 0) return false;
            if (!state.HasAllVotes) return TryResolveExpiredRound(state, DateTime.UtcNow);

            ResolveDecision(state);
            return true;
        }

        public bool HasEligiblePlayerClan(KingdomDecision decision)
        {
            if (decision != null &&
                DecisionStates.TryGetValue(decision, out KingdomDecisionVoteState state) &&
                state.HasRoundSnapshot)
            {
                return true;
            }

            return GetEligibleClanIds(decision).Count > 0;
        }

        public bool TryPublishFinalVoteForElection(KingdomElection election)
        {
            KingdomDecision decision = election?._decision;
            if (decision == null || !IsLocalPlayerEligible(decision)) return false;
            if (!TryGetDecisionIndex(decision, out int decisionIndex)) return false;
            if (!TryGetKingdomId(decision.Kingdom, out string kingdomId)) return false;

            DecisionOutcome chosenOutcome = election._chosenOutcome;
            bool isAbstain = chosenOutcome == null;
            int outcomeIndex = -1;
            string outcomeKey = null;

            if (!isAbstain)
            {
                outcomeIndex = GetOutcomeIndex(chosenOutcome, election);
                if (outcomeIndex < 0) return false;
                outcomeResolver.TryGetOutcomeKey(chosenOutcome, objectManager, out outcomeKey);
            }

            var voteData = new KingdomDecisionVoteData(
                kingdomId,
                decisionIndex,
                outcomeIndex,
                GetSupportWeightValue(isAbstain, Supporter.SupportWeights.FullyPush),
                isAbstain,
                true,
                outcomeKey);

            TryApplyLocalVote(decision, voteData);
            MessageBroker.Instance.Publish(election, new KingdomDecisionVoteRequested(voteData));
            LocalSubmittedDecisions.Add(decision);
            return true;
        }

        public IReadOnlyList<KingdomDecisionDebugInfo> GetDecisionDebugInfo(Kingdom kingdom)
        {
            List<KingdomDecisionDebugInfo> decisionInfos = new List<KingdomDecisionDebugInfo>();
            if (kingdom?._unresolvedDecisions == null) return decisionInfos;

            foreach (KingdomDecision decision in kingdom._unresolvedDecisions.ToList())
            {
                if (decision == null) continue;
                KingdomDecisionVoteState state = GetOrCreateState(decision);
                RefreshEligibleClanIds(state, decision);
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
                ClearDecisionState(kingdomId, decisionIndex);
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
            CloseDecision(decision);
        }

        public void ClearDecisionState(string kingdomId, int decisionIndex)
        {
            if (string.IsNullOrWhiteSpace(kingdomId) || decisionIndex < 0) return;

            PendingRoundStatuses.RemoveAll(candidate => candidate.KingdomId == kingdomId);
            if (!TryGetDecision(kingdomId, decisionIndex, out KingdomDecision decision)) return;

            RemoveDecisionState(decision);
        }

        public void ClearDecisionState(KingdomDecision decision)
        {
            RemoveDecisionState(decision);
        }

        public void CloseDecision(string kingdomId, int decisionIndex)
        {
            if (!TryGetDecision(kingdomId, decisionIndex, out KingdomDecision decision)) return;

            CloseDecision(decision);
        }

        private void CloseDecision(KingdomDecision decision)
        {
            foreach (DecisionItemBaseVM decisionItem in ActiveDecisionItems
                         .Where(item => item?.KingdomDecisionMaker?._decision == decision)
                         .ToList())
            {
                CloseDecisionItem(decisionItem);
            }
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
            if (state.FinalVotes.ContainsKey(clanId)) return false;

            if (!ApplyVoteToElection(state.Election, clan, voteData))
            {
                return false;
            }

            state.Votes[clanId] = new AppliedKingdomDecisionVote(clanId, voteData);
            if (voteData.IsFinal)
            {
                state.FinalVotes[clanId] = new AppliedKingdomDecisionVote(clanId, voteData);
                if (state.RoundClans.TryGetValue(clanId, out KingdomDecisionRoundClanStatusData roundClan))
                {
                    state.RoundClans[clanId] = new KingdomDecisionRoundClanStatusData(
                        roundClan.ClanId,
                        roundClan.ClanName,
                        roundClan.PlayerNames,
                        true,
                        roundClan.IsConnected);
                }
            }
            ApplyVotesToActiveDecisionItems(state);
            return true;
        }

        private bool TryApplyLocalVote(KingdomDecision decision, KingdomDecisionVoteData voteData)
        {
            if (decision == null || Clan.PlayerClan == null) return false;
            if (!TryGetClanId(Clan.PlayerClan, out string clanId)) return false;
            KingdomDecisionVoteState state = GetOrCreateState(decision);
            RefreshEligibleClanIds(state, decision);
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

            if (state.Decision is StartAllianceDecision allianceDecision && CoopKingdomElection.TryRedirectPlayerAllianceOffer(state.Decision, chosenOutcome)
                && chosenOutcome is StartAllianceDecision.StartAllianceDecisionOutcome startAllianceOutcome && startAllianceOutcome.ShouldAllianceBeStarted)
            {
                if (allianceDecision.Kingdom is Kingdom playerKingdom && playerKingdom.IsPlayerKingdom()
                    && allianceDecision.KingdomToStartAllianceWith is Kingdom playerkingdom2 && playerkingdom2.IsPlayerKingdom())
                {
                    messageBroker?.Publish(state.Decision, new AllianceOfferPendingStatusChanged(
                        allianceDecision.Kingdom,
                        allianceDecision.KingdomToStartAllianceWith,
                        isPending: true));
                }
                if (state.Decision.Kingdom._unresolvedDecisions.Contains(state.Decision))
                {
                    state.Decision.Kingdom.RemoveDecision(state.Decision);
                }
                RemoveDecisionState(state.Decision);
                return;
            }

            if (state.Decision is ProposeCallToWarAgreementDecision && CoopKingdomElection.TryRedirectPlayerProposeCallToWarAgreementOffer(state.Decision, chosenOutcome)
                && chosenOutcome is ProposeCallToWarAgreementDecision.ProposeCallToWarAgreementDecisionOutcome proposeCallToWarOutcome && proposeCallToWarOutcome.ShouldCallToWar)
            {
                if (state.Decision.Kingdom._unresolvedDecisions.Contains(state.Decision))
                {
                    state.Decision.Kingdom.RemoveDecision(state.Decision);
                }
                RemoveDecisionState(state.Decision);
                return;
            }
            if (state.Decision is MakePeaceKingdomDecision peaceDecision && CoopKingdomElection.TryRedirectPlayerPeaceOffer(state.Decision, chosenOutcome)
                && chosenOutcome is MakePeaceKingdomDecision.MakePeaceDecisionOutcome peaceOutcome && peaceOutcome.ShouldPeaceBeDeclared)
            {
                if (peaceDecision.Kingdom is Kingdom playerKingdom && playerKingdom.IsPlayerKingdom()
                    && peaceDecision.FactionToMakePeaceWith is Kingdom playerkingdom2 && playerkingdom2.IsPlayerKingdom())
                {
                    messageBroker?.Publish(state.Decision, new PeaceOfferPendingStatusChanged(
                        peaceDecision.Kingdom,
                        (Kingdom)peaceDecision.FactionToMakePeaceWith,
                        isPending: true));
                }

                if (state.Decision.Kingdom._unresolvedDecisions.Contains(state.Decision))
                {
                    state.Decision.Kingdom.RemoveDecision(state.Decision);
                }
                RemoveDecisionState(state.Decision);
                return;
            }
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

            if (state.Decision is MakePeaceKingdomDecision decision)
            {
                if (decision.Kingdom is Kingdom playerKingdom && playerKingdom.IsPlayerKingdom() 
                    && decision.FactionToMakePeaceWith is Kingdom playerkingdom2 && playerkingdom2.IsPlayerKingdom())
                {
                    messageBroker?.Publish(state.Decision, new PeaceOfferPendingStatusChanged(
                        playerkingdom2,
                        playerKingdom,
                        isPending: false));
                }
            }
            if (state.Decision is StartAllianceDecision startAllianceDecision)
            {
                if (CoopKingdomElection._opponentProposedAllianceDecisions.Contains(startAllianceDecision))
                {
                    CoopKingdomElection._opponentProposedAllianceDecisions.Remove(startAllianceDecision);
                }
                if (startAllianceDecision.Kingdom is Kingdom playerKingdom && playerKingdom.IsPlayerKingdom()
                    && startAllianceDecision.KingdomToStartAllianceWith is Kingdom playerkingdom2 && playerkingdom2.IsPlayerKingdom())
                {
                    messageBroker?.Publish(state.Decision, new AllianceOfferPendingStatusChanged(
                         playerkingdom2,
                         playerKingdom,
                         isPending: false));
                }
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
            HashSet<string> eligibleClanIds = GetEligibleClanIds(decision);
            DateTime? deadlineUtc = ModInformation.IsServer && eligibleClanIds.Count > 0
                ? DateTime.UtcNow + VotingRoundDuration
                : null;
            Dictionary<string, KingdomDecisionRoundClanStatusData> roundClans = ModInformation.IsServer
                ? CreateRoundClanStatuses(decision, eligibleClanIds)
                : new Dictionary<string, KingdomDecisionRoundClanStatusData>();
            return new KingdomDecisionVoteState(
                kingdomId,
                decisionIndex,
                decision,
                eligibleClanIds,
                deadlineUtc,
                roundClans);
        }

        private HashSet<string> GetEligibleClanIds(KingdomDecision decision)
        {
            HashSet<string> eligibleClanIds = new HashSet<string>();
            if (playerManager == null || objectManager == null) return eligibleClanIds;

            foreach (var player in playerManager.Players)
            {
                if (ModInformation.IsServer && !playerManager.IsConnected(player)) continue;
                if (string.IsNullOrEmpty(player.ClanId)) continue;
                if (!TryGetClan(player.ClanId, decision.Kingdom, out Clan clan)) continue;
                if (clan.Kingdom != decision.Kingdom) continue;
                if (clan.IsUnderMercenaryService) continue;

                if (TryGetClanId(clan, out string clanId))
                {
                    eligibleClanIds.Add(clanId);
                }
            }
            return eligibleClanIds;
        }

        private void RefreshEligibleClanIds(KingdomDecisionVoteState state, KingdomDecision decision)
        {
            if (state == null || decision == null) return;

            if (state.HasRoundSnapshot)
            {
                state.RefreshEligibleClanIds(new HashSet<string>(state.RoundClans.Keys));
                return;
            }

            state.RefreshEligibleClanIds(GetEligibleClanIds(decision));
        }

        private Dictionary<string, KingdomDecisionRoundClanStatusData> CreateRoundClanStatuses(
            KingdomDecision decision,
            IEnumerable<string> eligibleClanIds)
        {
            var result = new Dictionary<string, KingdomDecisionRoundClanStatusData>();
            if (decision == null || playerManager == null) return result;

            foreach (string clanId in eligibleClanIds)
            {
                result[clanId] = CreateRoundClanStatus(decision, clanId, false);
            }
            return result;
        }

        private KingdomDecisionRoundClanStatusData CreateRoundClanStatus(
            KingdomDecision decision,
            string clanId,
            bool hasFinalVote)
        {
            string clanName = clanId;
            if (TryGetClan(clanId, decision.Kingdom, out Clan clan))
            {
                clanName = clan.Name?.ToString() ?? clan.StringId ?? clanId;
            }

            Player[] clanPlayers = playerManager.Players
                .Where(player => player.ClanId == clanId || PlayerBelongsToClan(player, decision.Kingdom, clanId))
                .ToArray();
            string playerNames = string.Join(", ", clanPlayers
                .Select(GetPlayerDisplayName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .OrderBy(name => name));
            bool isConnected = clanPlayers.Any(playerManager.IsConnected);

            return new KingdomDecisionRoundClanStatusData(
                clanId,
                clanName,
                string.IsNullOrWhiteSpace(playerNames) ? clanName : playerNames,
                hasFinalVote,
                isConnected);
        }

        private bool PlayerBelongsToClan(Player player, Kingdom kingdom, string canonicalClanId)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.ClanId)) return false;
            if (!TryGetClan(player.ClanId, kingdom, out Clan clan)) return false;
            return TryGetClanId(clan, out string clanId) && clanId == canonicalClanId;
        }

        private string GetPlayerDisplayName(Player player)
        {
            if (player != null &&
                !string.IsNullOrWhiteSpace(player.HeroId) &&
                objectManager.TryGetObject<Hero>(player.HeroId, out Hero hero) &&
                hero?.Name != null)
            {
                string heroName = hero.Name.ToString();
                if (!string.IsNullOrWhiteSpace(heroName)) return heroName;
            }

            return player?.ControllerId;
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

            return objectManager != null && objectManager.TryGetObject(clanId, out clan);
        }

        private bool TryGetClanId(Clan clan, out string clanId)
        {
            clanId = null;
            if (clan == null) return false;

            return objectManager != null && objectManager.TryGetId(clan, out clanId);
        }

        private bool IsLocalPlayerEligible(KingdomDecision decision)
        {
            if (decision == null || Clan.PlayerClan == null) return false;
            if (Clan.PlayerClan.Kingdom != decision.Kingdom) return false;

            if (DecisionStates.TryGetValue(decision, out KingdomDecisionVoteState state) && state.HasRoundSnapshot)
            {
                return TryGetClanId(Clan.PlayerClan, out string clanId) && state.EligibleClanIds.Contains(clanId);
            }

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

            return objectManager != null && objectManager.TryGetId(kingdom, out kingdomId);
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

        internal void ProcessVotingRounds(DateTime utcNow)
        {
            RegisterLoadedDecisions();
            foreach (KingdomDecisionVoteState state in DecisionStates.Values.ToList())
            {
                if (!IsDecisionUnresolved(state.Decision))
                {
                    RemoveDecisionState(state.Decision);
                    continue;
                }
                if (state.IsResolved || !state.RoundDeadlineUtc.HasValue) continue;

                TryGetKingdomId(state.Decision.Kingdom, out string kingdomId);
                TryGetDecisionIndex(state.Decision, out int decisionIndex);
                state.RefreshDecisionIdentity(kingdomId, decisionIndex);

                if (TryResolveExpiredRound(state, utcNow)) continue;

                PublishRoundStatus(state);
            }
        }

        private void RegisterLoadedDecisions()
        {
            if (!ModInformation.IsServer || Campaign.Current?.KingdomManager == null) return;

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom?._unresolvedDecisions == null) continue;

                foreach (KingdomDecision decision in kingdom.UnresolvedDecisions.ToList())
                {
                    if (decision == null || DecisionStates.ContainsKey(decision)) continue;
                    if (!HasEligiblePlayerClan(decision)) continue;

                    RegisterDecision(decision);
                }
            }
        }

        private void QueueVotingRoundTick()
        {
            if (Volatile.Read(ref isDisposed) != 0) return;

            if (!GameThread.Instance.IsInitialized)
            {
                return;
            }

            GameThread.RunSafe(
                () =>
                {
                    if (Volatile.Read(ref isDisposed) == 0)
                    {
                        ProcessVotingRounds(DateTime.UtcNow);
                    }
                },
                context: nameof(KingdomDecisionVoteManager));
        }

        private bool TryResolveExpiredRound(KingdomDecisionVoteState state, DateTime utcNow)
        {
            if (!state.RoundDeadlineUtc.HasValue || utcNow < state.RoundDeadlineUtc.Value) return false;

            Logger.Information(
                "Kingdom decision voting deadline reached for {KingdomId} decision {DecisionIndex}; resolving with {FinalVotes}/{EligibleClans} final clan votes",
                state.KingdomId,
                state.DecisionIndex,
                state.FinalVotes.Count,
                state.EligibleClanIds.Count);
            ApplyMissingAbstentions(state);
            ResolveDecision(state);
            return true;
        }

        private static bool IsDecisionUnresolved(KingdomDecision decision)
        {
            return decision?.Kingdom?._unresolvedDecisions?.Contains(decision) == true;
        }

        private void ApplyMissingAbstentions(KingdomDecisionVoteState state)
        {
            foreach (string clanId in state.EligibleClanIds.Where(clanId => !state.FinalVotes.ContainsKey(clanId)))
            {
                if (!TryGetClan(clanId, state.Decision.Kingdom, out Clan clan)) continue;

                ResetClanSupport(state.Election, clan);
                if (state.Election._chooser == clan)
                {
                    state.Election._chosenOutcome = null;
                }
            }
            state.Election.DetermineOfficialSupport();
        }

        private void PublishRoundStatus(KingdomDecisionVoteState state)
        {
            KingdomDecisionRoundStatusData status = CreateRoundStatus(state);
            if (status == null) return;
            if (state.LastPublishedRoundStatus != null &&
                state.LastPublishedRoundStatus.HasSameContent(status))
            {
                return;
            }

            state.LastPublishedRoundStatus = status;
            messageBroker?.Publish(state.Decision, new KingdomDecisionRoundStatusChanged(status));
        }

        private KingdomDecisionRoundStatusData CreateRoundStatus(KingdomDecisionVoteState state)
        {
            if (!ModInformation.IsServer || state == null || state.IsResolved || !state.RoundDeadlineUtc.HasValue) return null;

            foreach (string clanId in state.EligibleClanIds)
            {
                state.RoundClans[clanId] = CreateRoundClanStatus(
                    state.Decision,
                    clanId,
                    state.FinalVotes.ContainsKey(clanId));
            }

            return new KingdomDecisionRoundStatusData(
                state.KingdomId,
                state.DecisionIndex,
                state.RoundDeadlineUtc.Value.Ticks,
                state.RoundClans.Values.OrderBy(clan => clan.ClanId).ToArray(),
                outcomeOrder.CaptureKeys(state.Election._possibleOutcomes, objectManager));
        }

        private void ShowSubmittedState(DecisionItemBaseVM decisionItem)
        {
            if (decisionItem == null) return;

            decisionItem._finalSelectionDone = true;
            decisionItem.RefreshCanEndDecision();
            foreach (DecisionOptionVM option in decisionItem.DecisionOptionsList)
            {
                option.CanBeChosen = false;
                option.IsSupportOption1Enabled = false;
                option.IsSupportOption2Enabled = false;
                option.IsSupportOption3Enabled = false;
            }
            RefreshDecisionPresentation(decisionItem);
        }

        private void RefreshDecisionPresentation(DecisionItemBaseVM decisionItem)
        {
            RefreshDecisionTitle(decisionItem);
            RefreshDecisionWaitingStatus(decisionItem);
        }

        private KingdomDecisionWaitingFeedback GetWaitingFeedback(DecisionItemBaseVM decisionItem)
        {
            KingdomDecision decision = decisionItem?.KingdomDecisionMaker?._decision;
            if (decision == null || !DecisionStates.TryGetValue(decision, out KingdomDecisionVoteState state)) return null;
            if (state.RoundClans.Count == 0 || !state.RoundDeadlineUtc.HasValue) return null;

            List<KingdomDecisionRoundClanStatusData> waitingClans = state.RoundClans.Values
                .Where(clan => !clan.HasFinalVote)
                .OrderBy(clan => clan.PlayerNames)
                .ThenBy(clan => clan.ClanName)
                .ToList();

            return roundPresentation.FormatWaitingFeedback(HasLocalPlayerSubmittedVote(decision), waitingClans);
        }

        private int? TryGetRemainingSeconds(DecisionItemBaseVM decisionItem)
        {
            KingdomDecision decision = decisionItem?.KingdomDecisionMaker?._decision;
            if (decision == null || !DecisionStates.TryGetValue(decision, out KingdomDecisionVoteState state)) return null;
            if (!state.RoundDeadlineUtc.HasValue) return null;

            return Math.Max(
                0,
                (int)Math.Ceiling((state.RoundDeadlineUtc.Value - DateTime.UtcNow).TotalSeconds));
        }

        private static string GetBaseDecisionTitle(DecisionItemBaseVM decisionItem)
        {
            KingdomElection election = decisionItem?.KingdomDecisionMaker;
            KingdomDecision decision = election?._decision;
            if (decision == null) return decisionItem.TitleText;

            string baseTitle = election._chooser == Clan.PlayerClan
                ? decision.GetChooseTitle()?.ToString()
                : decision.GetSupportTitle()?.ToString();
            return baseTitle ?? string.Empty;
        }

        private void ApplyAuthoritativeOutcomes(KingdomDecisionVoteState state, DecisionItemBaseVM decisionItem = null)
        {
            if (state?.OrderedOutcomeKeys == null || state.OrderedOutcomeKeys.Length == 0) return;

            if (state.Election != null && !HasAuthoritativeOutcomeOrder(state.Election, state.OrderedOutcomeKeys))
            {
                IReadOnlyList<DecisionOutcome> orderedOutcomes = outcomeOrder.ResolveOrderedOutcomes(
                    state.OrderedOutcomeKeys,
                    state.Election._possibleOutcomes,
                    GetFullDecisionCandidates(state.Decision),
                    objectManager);
                if (orderedOutcomes.Count == 0) return;

                InitializeAuthoritativeOutcomes(state.Election, orderedOutcomes);
                ReplayVotes(state.Election, state);
            }

            if (decisionItem == null) return;

            KingdomElection election = decisionItem.KingdomDecisionMaker;
            if (election != null && !HasAuthoritativeOutcomeOrder(election, state.OrderedOutcomeKeys))
            {
                IReadOnlyList<DecisionOutcome> itemOutcomes = outcomeOrder.ResolveOrderedOutcomes(
                    state.OrderedOutcomeKeys,
                    election._possibleOutcomes,
                    GetFullDecisionCandidates(election._decision),
                    objectManager);
                if (itemOutcomes.Count > 0)
                {
                    InitializeAuthoritativeOutcomes(election, itemOutcomes);
                    RemapDecisionOptions(decisionItem, itemOutcomes);
                }
            }

            ReplayVotes(decisionItem, state);
        }

        private bool HasAuthoritativeOutcomeOrder(
            KingdomElection election,
            IReadOnlyList<string> orderedOutcomeKeys)
        {
            if (election?._possibleOutcomes == null || orderedOutcomeKeys == null) return false;

            string[] localKeys = outcomeOrder.CaptureKeys(election._possibleOutcomes, objectManager);
            return localKeys.Length == orderedOutcomeKeys.Count &&
                   localKeys.SequenceEqual(orderedOutcomeKeys);
        }

        private static void InitializeAuthoritativeOutcomes(
            KingdomElection election,
            IReadOnlyList<DecisionOutcome> outcomes)
        {
            MBList<DecisionOutcome> outcomeList = ToMBList(outcomes);
            election._possibleOutcomes = outcomeList;
            election._decision.DetermineSponsors(outcomeList);

            foreach (DecisionOutcome outcome in outcomeList)
            {
                outcome.InitialSupport = election.DetermineInitialSupport(outcome);
            }

            float totalInitialSupport = outcomeList.Sum(outcome => outcome.InitialSupport);
            foreach (DecisionOutcome outcome in outcomeList)
            {
                outcome.Likelihood = totalInitialSupport == 0f
                    ? 0f
                    : outcome.InitialSupport / totalInitialSupport;
            }

            election.DetermineSupport(outcomeList, false);
            election._decision.DetermineSponsors(outcomeList);
            election.UpdateSupport(outcomeList);
            election.DetermineOfficialSupport();
        }

        private void ReplayVotes(KingdomElection election, KingdomDecisionVoteState state)
        {
            foreach (AppliedKingdomDecisionVote vote in state.Votes.Values)
            {
                if (!TryGetClan(vote.ClanId, state.Decision.Kingdom, out Clan clan)) continue;

                ApplyVoteToElection(election, clan, vote.VoteData, false);
            }
        }

        private static MBList<DecisionOutcome> ToMBList(IReadOnlyList<DecisionOutcome> outcomes)
        {
            var list = new MBList<DecisionOutcome>();
            if (outcomes == null) return list;

            foreach (DecisionOutcome outcome in outcomes)
            {
                list.Add(outcome);
            }

            return list;
        }

        private static IEnumerable<DecisionOutcome> GetFullDecisionCandidates(KingdomDecision decision)
        {
            if (decision == null) return Array.Empty<DecisionOutcome>();

            MBList<DecisionOutcome> candidates =
                (decision.DetermineInitialCandidates() ?? Array.Empty<DecisionOutcome>()).ToMBList();
            return decision.NarrowDownCandidates(candidates, candidates.Count);
        }

        private void RemapDecisionOptions(DecisionItemBaseVM decisionItem, IReadOnlyList<DecisionOutcome> orderedOutcomes)
        {
            if (decisionItem?.DecisionOptionsList == null || orderedOutcomes == null) return;

            DecisionOptionVM selectedOption = decisionItem._currentSelectedOption;
            bool selectedOptionWasVisible = selectedOption != null &&
                                            decisionItem.DecisionOptionsList.Contains(selectedOption);
            string selectedKey = null;
            if (selectedOptionWasVisible && selectedOption.Option != null)
            {
                outcomeResolver.TryGetOutcomeKey(selectedOption.Option, objectManager, out selectedKey);
            }

            var existingByKey = new Dictionary<string, DecisionOptionVM>();
            DecisionOptionVM abstainOption = null;
            Action<DecisionOptionVM> onSelect = null;
            Action<DecisionOptionVM> onSupportChange = null;
            foreach (DecisionOptionVM option in decisionItem.DecisionOptionsList)
            {
                if (option == null) continue;
                if (onSelect == null) onSelect = option._onSelect;
                if (onSupportChange == null) onSupportChange = option._onSupportStrengthChange;
                if (option.IsOptionForAbstain)
                {
                    abstainOption = option;
                    continue;
                }

                if (option.Option == null) continue;
                if (!outcomeResolver.TryGetOutcomeKey(option.Option, objectManager, out string optionKey)) continue;
                if (string.IsNullOrWhiteSpace(optionKey) || existingByKey.ContainsKey(optionKey)) continue;

                existingByKey.Add(optionKey, option);
            }

            if (selectedOption != null && !selectedOption.IsOptionForAbstain)
            {
                selectedOption.IsSelected = false;
                decisionItem._currentSelectedOption = null;
            }

            decisionItem.DecisionOptionsList.Clear();
            foreach (DecisionOutcome outcome in orderedOutcomes)
            {
                if (outcome == null) continue;
                DecisionOptionVM option;
                if (outcomeResolver.TryGetOutcomeKey(outcome, objectManager, out string outcomeKey) &&
                    existingByKey.TryGetValue(outcomeKey, out option))
                {
                    option.Option = outcome;
                }
                else
                {
                    option = new DecisionOptionVM(
                        outcome,
                        decisionItem.KingdomDecisionMaker?._decision,
                        decisionItem.KingdomDecisionMaker,
                        onSelect ?? decisionItem.OnChangeVote,
                        onSupportChange ?? decisionItem.OnSupportStrengthChange);
                }

                option.WinPercentage = TaleWorlds.Library.MathF.Round(outcome.WinChance * 100f);
                option.InitialPercentage = TaleWorlds.Library.MathF.Round(outcome.WinChance * 100f);

                decisionItem.DecisionOptionsList.Add(option);
            }

            if (abstainOption != null)
            {
                decisionItem.DecisionOptionsList.Add(abstainOption);
            }

            if (!string.IsNullOrWhiteSpace(selectedKey))
            {
                foreach (DecisionOptionVM option in decisionItem.DecisionOptionsList)
                {
                    if (option?.Option == null) continue;
                    if (!outcomeResolver.TryGetOutcomeKey(option.Option, objectManager, out string optionKey)) continue;
                    if (!string.Equals(optionKey, selectedKey, StringComparison.Ordinal)) continue;

                    decisionItem._currentSelectedOption = option;
                    option.IsSelected = true;
                    break;
                }
            }
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref isDisposed, 1);
            votingRoundTimer?.Dispose();
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
            public Dictionary<string, KingdomDecisionRoundClanStatusData> RoundClans { get; }
            public string[] OrderedOutcomeKeys { get; private set; }
            public KingdomDecisionRoundStatusData LastPublishedRoundStatus { get; set; }
            public DateTime? RoundDeadlineUtc { get; private set; }
            public bool IsResolved { get; set; }
            public bool HasRoundSnapshot => RoundClans.Count > 0;

            public bool HasAllVotes => EligibleClanIds.Count > 0 && EligibleClanIds.All(clanId => FinalVotes.ContainsKey(clanId));

            public KingdomDecisionVoteState(
                string kingdomId,
                int decisionIndex,
                KingdomDecision decision,
                HashSet<string> eligibleClanIds,
                DateTime? roundDeadlineUtc,
                Dictionary<string, KingdomDecisionRoundClanStatusData> roundClans)
            {
                KingdomId = kingdomId;
                Decision = decision;
                DecisionIndex = decisionIndex;
                Election = new CoopKingdomElection(decision);
                Election.SetupPlayerVoteElection();
                EligibleClanIds = eligibleClanIds;
                Votes = new Dictionary<string, AppliedKingdomDecisionVote>();
                FinalVotes = new Dictionary<string, AppliedKingdomDecisionVote>();
                RoundDeadlineUtc = roundDeadlineUtc;
                RoundClans = roundClans ?? new Dictionary<string, KingdomDecisionRoundClanStatusData>();
                OrderedOutcomeKeys = Array.Empty<string>();
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

            public void ApplyRoundStatus(KingdomDecisionRoundStatusData status)
            {
                RoundDeadlineUtc = new DateTime(status.DeadlineUtcTicks, DateTimeKind.Utc);
                RoundClans.Clear();
                foreach (KingdomDecisionRoundClanStatusData clan in status.Clans ?? Array.Empty<KingdomDecisionRoundClanStatusData>())
                {
                    if (clan == null || string.IsNullOrWhiteSpace(clan.ClanId)) continue;
                    RoundClans[clan.ClanId] = clan;
                }
                OrderedOutcomeKeys = status.OrderedOutcomeKeys ?? Array.Empty<string>();
                RefreshEligibleClanIds(new HashSet<string>(RoundClans.Keys));
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
