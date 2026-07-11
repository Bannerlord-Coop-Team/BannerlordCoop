using Common.Logging;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using SandBox.Tournaments.MissionLogics;
using SandBox.ViewModelCollection.Tournament;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Tournaments.UI;

internal sealed class CoopTournamentVM : TournamentVM
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopTournamentVM>();

    internal readonly struct UIState
    {
        public readonly bool CanJoin;
        public readonly bool CanWatch;
        public readonly bool CanSkip;
        public readonly bool CanLeave;
        public readonly bool CanBet;
        public readonly bool IsMatchActive;
        public readonly int ReadyCount;
        public readonly int SkipCount;
        public readonly int VoterCount;
        public readonly TournamentPlayerChoice SelectedChoice;

        public UIState(
            bool canJoin,
            bool canWatch,
            bool canSkip,
            bool canLeave,
            bool canBet,
            bool isMatchActive,
            int readyCount,
            int skipCount,
            int voterCount,
            TournamentPlayerChoice selectedChoice)
        {
            CanJoin = canJoin;
            CanWatch = canWatch;
            CanSkip = canSkip;
            CanLeave = canLeave;
            CanBet = canBet;
            IsMatchActive = isMatchActive;
            ReadyCount = readyCount;
            SkipCount = skipCount;
            VoterCount = voterCount;
            SelectedChoice = selectedChoice;
        }
    }

    internal readonly struct BetSummary
    {
        public readonly int BettedDenars;
        public readonly int ThisRoundBettedDenars;
        public readonly int ExpectedPayout;
        public readonly bool IsSettlement;

        public BetSummary(
            int bettedDenars,
            int thisRoundBettedDenars,
            int expectedPayout,
            bool isSettlement)
        {
            BettedDenars = bettedDenars;
            ThisRoundBettedDenars = thisRoundBettedDenars;
            ExpectedPayout = expectedPayout;
            IsSettlement = isSettlement;
        }
    }

    private readonly ITournamentUIController controller;
    private readonly Action disableUI;
    private readonly TournamentCompletionPresentation completionPresentation = new();
    private readonly TournamentMatchPresentation matchPresentation;
    private TournamentSessionSnapshot snapshot;
    private bool canJoin;
    private bool canWatch;
    private bool canSkip;
    private bool canLeave;
    private bool canBet;
    private bool isJoinVisible;
    private bool isCoopMatchActive;
    private string readyCountText;
    private string skipCountText;
    private string selectedChoiceText;
    private bool pendingBracketRefresh;
    private bool hasAcceptedBetResult;
    private long acceptedBetSequence;
    private BetSummary acceptedBetSummary;

    public CoopTournamentVM(
        Action disableUI,
        TournamentBehavior tournamentBehavior,
        TournamentSessionSnapshot initialSnapshot,
        ITournamentUIController controller)
        : base(disableUI, tournamentBehavior)
    {
        this.controller = controller;
        this.disableUI = disableUI;
        snapshot = initialSnapshot;
        completionPresentation.Observe(initialSnapshot?.IsCompleted == true);
        matchPresentation = new TournamentMatchPresentation(initialSnapshot?.Phase == TournamentSessionPhase.LiveMatch);
        controller.StateChanged += HandleStateChanged;
        controller.SessionRemoved += HandleSessionRemoved;
        controller.BetResultReceived += HandleBetResultReceived;

        RefreshCoopState();
    }

    [DataSourceProperty]
    public bool CanJoin
    {
        get => canJoin;
        private set
        {
            if (canJoin == value) return;
            canJoin = value;
            OnPropertyChangedWithValue(value, nameof(CanJoin));
        }
    }

    [DataSourceProperty]
    public bool CanWatch
    {
        get => canWatch;
        private set
        {
            if (canWatch == value) return;
            canWatch = value;
            OnPropertyChangedWithValue(value, nameof(CanWatch));
        }
    }

    [DataSourceProperty]
    public bool CanSkip
    {
        get => canSkip;
        private set
        {
            if (canSkip == value) return;
            canSkip = value;
            OnPropertyChangedWithValue(value, nameof(CanSkip));
        }
    }

    [DataSourceProperty]
    public bool CanLeave
    {
        get => canLeave;
        private set
        {
            if (canLeave == value) return;
            canLeave = value;
            OnPropertyChangedWithValue(value, nameof(CanLeave));
        }
    }

    [DataSourceProperty]
    public bool IsJoinVisible
    {
        get => isJoinVisible;
        private set
        {
            if (isJoinVisible == value) return;
            isJoinVisible = value;
            OnPropertyChangedWithValue(value, nameof(IsJoinVisible));
        }
    }

    [DataSourceProperty]
    public bool IsCoopMatchActive
    {
        get => isCoopMatchActive;
        private set
        {
            if (isCoopMatchActive == value) return;
            isCoopMatchActive = value;
            base.IsCurrentMatchActive = value;
            OnPropertyChangedWithValue(value, nameof(IsCoopMatchActive));
            OnPropertyChanged(nameof(ShouldShowUI));
        }
    }

    [DataSourceProperty]
    public bool ShouldShowUI => TournamentMissionPresentationState
        .From(snapshot)
        .ShouldShowUI;

    [DataSourceProperty]
    public string ReadyCountText
    {
        get => readyCountText;
        private set
        {
            if (readyCountText == value) return;
            readyCountText = value;
            OnPropertyChangedWithValue(value, nameof(ReadyCountText));
        }
    }

    [DataSourceProperty]
    public string SkipCountText
    {
        get => skipCountText;
        private set
        {
            if (skipCountText == value) return;
            skipCountText = value;
            OnPropertyChangedWithValue(value, nameof(SkipCountText));
        }
    }

    [DataSourceProperty]
    public string SelectedChoiceText
    {
        get => selectedChoiceText;
        private set
        {
            if (selectedChoiceText == value) return;
            selectedChoiceText = value;
            OnPropertyChangedWithValue(value, nameof(SelectedChoiceText));
        }
    }

    [DataSourceProperty]
    public string JoinText => new TaleWorlds.Localization.TextObject("{=coop_tournament_join_match}Join").ToString();

    [DataSourceProperty]
    public string WatchText => new TaleWorlds.Localization.TextObject("{=coop_tournament_watch_match}Watch").ToString();

    [DataSourceProperty]
    public string SkipText => new TaleWorlds.Localization.TextObject("{=coop_tournament_skip_match}Skip").ToString();

    [DataSourceProperty]
    public string CoopLeaveText => new TaleWorlds.Localization.TextObject("{=coop_tournament_leave_active}Leave").ToString();

    [DataSourceProperty]
    public new bool IsTournamentIncomplete => false;

    [DataSourceProperty]
    public new bool IsBetButtonEnabled => canBet;

    public void ExecuteJoin()
    {
        if (CanJoin) controller.RequestChoice(snapshot, TournamentPlayerChoice.Join);
    }

    public void ExecuteWatch()
    {
        if (CanWatch) controller.RequestChoice(snapshot, TournamentPlayerChoice.Watch);
    }

    public void ExecuteSkip()
    {
        if (CanSkip) controller.RequestChoice(snapshot, TournamentPlayerChoice.Skip);
    }

    public void ExecuteOpenBetWindow()
    {
        if (IsBetButtonEnabled)
            IsBetWindowEnabled = true;
    }

    public new void ExecuteBet()
    {
        if (!IsBetButtonEnabled || WageredDenars <= 0) return;

        controller.RequestBet(snapshot, WageredDenars);
        IsBetWindowEnabled = false;
    }

    public new void ExecuteLeave()
    {
        if (!CanLeave) return;

        controller.RequestLeaveActive(snapshot);
    }

    public void RefreshPendingBracket()
    {
        if (pendingBracketRefresh)
        {
            pendingBracketRefresh = false;
            TournamentMatchVM currentMatch = RebindCanonicalBracket(
                new[] { Round1, Round2, Round3, Round4 },
                Tournament.Rounds,
                Tournament.CurrentMatch,
                index => GameTexts.FindText("str_tournament_round", index.ToString()));
            SetCanonicalCurrentMatch(currentMatch);
            ApplyCanonicalMatchStates(
                new[] { Round1, Round2, Round3, Round4 },
                Tournament.CurrentMatch);
            ApplyAcceptedBetResult();
        }

        ApplyLiveMatchState();

        completionPresentation.TryPresent(() =>
        {
            MBInformationManager.AddQuickInformation(
                new TextObject("{=tWzLqegB}Tournament is over."));
            OnTournamentEnd();
        });
    }

    internal void OnCoopAgentRemoved(Agent affectedAgent)
    {
        Logger.Information(
            "[Tournament] Live roster received removal in match={MatchId}: active={IsActive}, human={IsHuman}, hasOrigin={HasOrigin}, matchState={MatchState}, rows={RowCount}",
            snapshot?.CurrentMatchId,
            IsCoopMatchActive,
            affectedAgent?.IsHuman == true,
            affectedAgent?.Origin != null,
            CurrentMatch?.State,
            CurrentMatch?.GetParticipants().Count() ?? 0);
        if (!IsCoopMatchActive || affectedAgent?.IsHuman != true || affectedAgent.Origin == null)
            return;

        int descriptorSeed = affectedAgent.Origin.UniqueSeed;
        TournamentParticipantVM row = CurrentMatch?.GetParticipants().FirstOrDefault(candidate =>
            candidate.Participant?.Descriptor.UniqueSeed == descriptorSeed);
        bool wasDead = row?.IsDead == true;
        if (!MarkParticipantDead(CurrentMatch?.GetParticipants(), descriptorSeed))
        {
            Logger.Warning(
                "[Tournament] Live roster could not match removed agent seed={DescriptorSeed} in match={MatchId}; rows={RowSeeds}",
                descriptorSeed,
                snapshot?.CurrentMatchId,
                string.Join(",", CurrentMatch?.GetParticipants()
                    .Where(candidate => candidate.Participant != null)
                    .Select(candidate => candidate.Participant.Descriptor.UniqueSeed) ?? Array.Empty<int>()));
            return;
        }

        ApplyLiveMatchState();
        Logger.Information(
            "[Tournament] Live roster updated row in match={MatchId}: seed={DescriptorSeed}, wasDead={WasDead}, isDead={IsDead}, matchState={MatchState}",
            snapshot?.CurrentMatchId,
            descriptorSeed,
            wasDead,
            row.IsDead,
            CurrentMatch.State);
    }

    internal static bool MarkParticipantDead(IEnumerable<TournamentParticipantVM> participants, int descriptorSeed)
    {
        TournamentParticipantVM participant = participants?.FirstOrDefault(candidate =>
            candidate.Participant?.Descriptor.UniqueSeed == descriptorSeed);
        if (participant == null)
            return false;

        participant.IsDead = true;
        return true;
    }

    private void ApplyLiveMatchState()
    {
        if (IsCoopMatchActive && CurrentMatch != null)
            CurrentMatch.State = 3;
    }

    private void SetCanonicalCurrentMatch(TournamentMatchVM currentMatch)
    {
        if (ReferenceEquals(_currentMatch, currentMatch)) return;

        _currentMatch = currentMatch;
        OnPropertyChangedWithValue(currentMatch, nameof(CurrentMatch));
    }

    internal static TournamentMatchVM RebindCanonicalBracket(
        IReadOnlyList<TournamentRoundVM> roundViewModels,
        IReadOnlyList<TournamentRound> canonicalRounds,
        TournamentMatch canonicalCurrentMatch,
        Func<int, TextObject> getRoundTitle)
    {
        if (roundViewModels == null) throw new ArgumentNullException(nameof(roundViewModels));
        if (canonicalRounds == null) throw new ArgumentNullException(nameof(canonicalRounds));
        if (getRoundTitle == null) throw new ArgumentNullException(nameof(getRoundTitle));
        if (roundViewModels.Count != canonicalRounds.Count)
            throw new ArgumentException("The canonical tournament bracket must contain all native rounds.", nameof(canonicalRounds));

        TournamentMatchVM currentMatchViewModel = null;
        for (int roundIndex = 0; roundIndex < roundViewModels.Count; roundIndex++)
        {
            TournamentRoundVM roundViewModel = roundViewModels[roundIndex];
            TournamentRound canonicalRound = canonicalRounds[roundIndex];
            ResetRoundViewModel(roundViewModel);
            roundViewModel.Initialize(canonicalRound, getRoundTitle(roundIndex));

            for (int matchIndex = 0; matchIndex < canonicalRound.Matches.Length; matchIndex++)
            {
                TournamentMatchVM matchViewModel = roundViewModel.Matches[matchIndex];
                RefreshCanonicalMatch(matchViewModel);
                matchViewModel.State = GetCanonicalMatchState(
                    canonicalRound.Matches[matchIndex],
                    canonicalCurrentMatch);

                if (ReferenceEquals(canonicalRound.Matches[matchIndex], canonicalCurrentMatch))
                    currentMatchViewModel = matchViewModel;
            }
        }

        return currentMatchViewModel;
    }

    private static void ResetRoundViewModel(TournamentRoundVM roundViewModel)
    {
        roundViewModel.IsValid = false;
        foreach (TournamentMatchVM matchViewModel in roundViewModel.Matches)
        {
            matchViewModel.IsValid = false;
            foreach (TournamentTeamVM teamViewModel in matchViewModel.Teams)
            {
                teamViewModel.IsValid = false;
                foreach (TournamentParticipantVM participantViewModel in teamViewModel.Participants)
                {
                    participantViewModel.IsInitialized = false;
                    participantViewModel.IsValid = false;
                }
            }
        }
    }

    internal static void RefreshCanonicalMatch(TournamentMatchVM matchViewModel)
    {
        foreach (TournamentTeamVM teamViewModel in matchViewModel.Teams.Take(matchViewModel.Count))
        {
            foreach (TournamentParticipantVM participantViewModel in teamViewModel.Participants.Take(teamViewModel.Count))
            {
                TournamentParticipant participant = participantViewModel.Participant;
                if (participant == null)
                    continue;

                participantViewModel.Score = participant.Score.ToString();
                participantViewModel.IsQualifiedForNextRound = matchViewModel.Match.Winners.Contains(participant);
            }
        }
    }

    internal static void ApplyCanonicalMatchStates(
        IReadOnlyList<TournamentRoundVM> roundViewModels,
        TournamentMatch canonicalCurrentMatch)
    {
        foreach (TournamentRoundVM roundViewModel in roundViewModels)
        {
            for (int matchIndex = 0; matchIndex < roundViewModel.Count; matchIndex++)
            {
                TournamentMatchVM matchViewModel = roundViewModel.Matches[matchIndex];
                matchViewModel.State = GetCanonicalMatchState(
                    matchViewModel.Match,
                    canonicalCurrentMatch);
            }
        }
    }

    private static int GetCanonicalMatchState(
        TournamentMatch match,
        TournamentMatch canonicalCurrentMatch)
    {
        if (match.State == TournamentMatch.MatchState.Finished) return 2;
        if (match.State == TournamentMatch.MatchState.Started) return 3;
        return ReferenceEquals(match, canonicalCurrentMatch) ? 1 : 0;
    }

    public override void RefreshValues()
    {
        base.RefreshValues();

        if (controller != null)
        {
            RefreshCoopState();
            ApplyAcceptedBetResult();
        }
    }

    public override void OnFinalize()
    {
        controller.StateChanged -= HandleStateChanged;
        controller.SessionRemoved -= HandleSessionRemoved;
        controller.BetResultReceived -= HandleBetResultReceived;
        base.OnFinalize();
    }

    private void HandleSessionRemoved(string sessionId)
    {
        if (snapshot?.SessionId == sessionId)
            Mission.Current?.EndMission();
    }

    private void HandleStateChanged(TournamentSessionSnapshot updatedSnapshot)
    {
        if (updatedSnapshot.SessionId != snapshot.SessionId || updatedSnapshot.Revision < snapshot.Revision)
            return;

        var bracketChanged = updatedSnapshot.BracketRevision != snapshot.BracketRevision;
        var matchChanged = updatedSnapshot.CurrentMatchId != snapshot.CurrentMatchId;
        snapshot = updatedSnapshot;
        OnPropertyChanged(nameof(ShouldShowUI));
        completionPresentation.Observe(updatedSnapshot.IsCompleted);
        if (matchChanged)
        {
            hasAcceptedBetResult = false;
            acceptedBetSummary = default;
            _thisRoundBettedAmount = 0;
        }
        RefreshCoopState();
        if (matchPresentation.Observe(IsCoopMatchActive))
            disableUI();

        if (bracketChanged)
            pendingBracketRefresh = true;
    }

    private void HandleBetResultReceived(NetworkTournamentBetResult result)
    {
        if (!TryGetAcceptedBetSummary(
                snapshot,
                result,
                acceptedBetSequence,
                out var summary))
        {
            return;
        }

        acceptedBetSequence = result.Sequence;
        acceptedBetSummary = summary;
        hasAcceptedBetResult = true;
        ApplyAcceptedBetResult();
    }

    private void ApplyAcceptedBetResult()
    {
        if (!hasAcceptedBetResult) return;

        Tournament.BettedDenars = acceptedBetSummary.BettedDenars;
        Tournament.OverallExpectedDenars = acceptedBetSummary.ExpectedPayout;
        _thisRoundBettedAmount = acceptedBetSummary.ThisRoundBettedDenars;
        MaximumBetValue = GetRemainingBetValue(
            Tournament.GetMaximumBet(),
            _thisRoundBettedAmount,
            Tournament.PlayerDenars);
        WageredDenars = 0;
        ExpectedBetDenars = 0;
        BettedDenarsText = new TaleWorlds.Localization.TextObject(
                "{=L9GnQvsq}Stake: {BETTED_DENARS}")
            .SetTextVariable("BETTED_DENARS", acceptedBetSummary.BettedDenars)
            .ToString();
        OverallExpectedDenarsText = new TaleWorlds.Localization.TextObject(
                "{=xzzSaN4b}Expected: {OVERALL_EXPECTED_DENARS}")
            .SetTextVariable("OVERALL_EXPECTED_DENARS", acceptedBetSummary.ExpectedPayout)
            .ToString();
        RefreshCoopState();
    }

    internal static bool TryGetAcceptedBetSummary(
        TournamentSessionSnapshot currentSnapshot,
        NetworkTournamentBetResult result,
        long previousAcceptedSequence,
        out BetSummary summary)
    {
        summary = default;
        if (currentSnapshot == null ||
            result.SessionId != currentSnapshot.SessionId ||
            !result.Accepted ||
            result.Sequence <= previousAcceptedSequence ||
            (!result.IsSettlement && result.MatchId != currentSnapshot.CurrentMatchId))
        {
            return false;
        }

        summary = result.IsSettlement
            ? new BetSummary(0, 0, 0, true)
            : new BetSummary(
                result.BettedDenars,
                result.ThisRoundBettedDenars,
                result.ExpectedPayout,
                false);
        return true;
    }

    internal static int GetRemainingBetValue(
        int maximumBet,
        int thisRoundBettedAmount,
        int playerDenars)
    {
        return Math.Min(
            Math.Max(maximumBet - thisRoundBettedAmount, 0),
            Math.Max(playerDenars, 0));
    }

    internal static UIState CalculateUIState(
        TournamentSessionSnapshot currentSnapshot,
        string localControllerId,
        bool hasRemainingBet)
    {
        bool awaitingChoice = currentSnapshot?.Phase == TournamentSessionPhase.AwaitingChoices;
        TournamentContestantData localContestant = GetLocalContestant(
            currentSnapshot,
            localControllerId);
        TournamentMatchData currentMatch = GetCurrentMatch(currentSnapshot);
        bool localIsInCurrentMatch = localContestant != null && currentMatch?.Teams.Any(team =>
            team.ParticipantSlotIds.Contains(localContestant.SlotId)) == true;
        bool localIsSpectator = currentSnapshot?.SpectatorControllerIds.Contains(localControllerId) == true;
        bool localIsVoter = localContestant != null || localIsSpectator;
        TournamentPlayerChoice selectedChoice = currentSnapshot?.Choices
            .FirstOrDefault(item => item.ControllerId == localControllerId)?.Choice ??
            TournamentPlayerChoice.None;

        return new UIState(
            awaitingChoice && localIsVoter && localIsInCurrentMatch,
            awaitingChoice && localIsVoter && !localIsInCurrentMatch,
            awaitingChoice && localIsVoter && currentSnapshot.SkipAllowed,
            currentSnapshot != null && (localIsVoter || currentSnapshot.IsCompleted),
            awaitingChoice && localIsInCurrentMatch && hasRemainingBet,
            currentSnapshot?.Phase == TournamentSessionPhase.LiveMatch,
            currentSnapshot?.ReadyCount ?? 0,
            currentSnapshot?.SkipCount ?? 0,
            currentSnapshot?.VoterCount ?? 0,
            selectedChoice);
    }

    private static TournamentContestantData GetLocalContestant(
        TournamentSessionSnapshot currentSnapshot,
        string localControllerId)
    {
        return currentSnapshot?.Contestants.FirstOrDefault(contestant =>
            contestant.IsHuman &&
            !contestant.IsReplaced &&
            contestant.ControllerId == localControllerId);
    }

    private static TournamentMatchData GetCurrentMatch(TournamentSessionSnapshot currentSnapshot)
    {
        return currentSnapshot?.Rounds
            .SelectMany(round => round.Matches)
            .FirstOrDefault(match => match.MatchId == currentSnapshot.CurrentMatchId);
    }
    private void RefreshCoopState()
    {
        bool hasRemainingBet = snapshot != null && GetRemainingBetValue(
            Tournament.GetMaximumBet(),
            _thisRoundBettedAmount,
            Tournament.PlayerDenars) > 0;
        UIState state = CalculateUIState(snapshot, controller.LocalControllerId, hasRemainingBet);

        CanJoin = state.CanJoin;
        IsJoinVisible = state.CanJoin;
        CanWatch = state.CanWatch;
        CanSkip = state.CanSkip;
        CanLeave = state.CanLeave;
        canBet = state.CanBet;
        IsCoopMatchActive = state.IsMatchActive;

        ReadyCountText = new TaleWorlds.Localization.TextObject(
                "{=coop_tournament_ready_count}{READY}/{TOTAL} players ready")
            .SetTextVariable("READY", state.ReadyCount)
            .SetTextVariable("TOTAL", state.VoterCount)
            .ToString();
        SkipCountText = new TaleWorlds.Localization.TextObject(
                "{=coop_tournament_skip_count}{SKIP}/{TOTAL} players vote skip")
            .SetTextVariable("SKIP", state.SkipCount)
            .SetTextVariable("TOTAL", state.VoterCount)
            .ToString();
        SelectedChoiceText = GetSelectedChoiceText(state.SelectedChoice);

        OnPropertyChanged(nameof(IsBetButtonEnabled));
    }

    private static string GetSelectedChoiceText(TournamentPlayerChoice choice)
    {
        return choice switch
        {
            TournamentPlayerChoice.Join => new TaleWorlds.Localization.TextObject("{=coop_tournament_selected_join}Selected: Join").ToString(),
            TournamentPlayerChoice.Watch => new TaleWorlds.Localization.TextObject("{=coop_tournament_selected_watch}Selected: Watch").ToString(),
            TournamentPlayerChoice.Skip => new TaleWorlds.Localization.TextObject("{=coop_tournament_selected_skip}Selected: Skip").ToString(),
            _ => new TaleWorlds.Localization.TextObject("{=coop_tournament_selected_none}No choice selected").ToString()
        };
    }
}
