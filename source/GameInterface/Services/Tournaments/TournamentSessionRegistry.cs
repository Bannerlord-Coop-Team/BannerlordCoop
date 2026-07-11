using GameInterface.Services.Tournaments.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Tournaments;

public enum TournamentMutationStatus
{
    Applied,
    NoChange,
    NotFound,
    StaleRevision,
    InvalidPhase,
    Full,
    NotParticipant,
    InvalidChoice,
    Rejected
}

public enum TournamentBallotOutcome
{
    Open,
    StartLiveMatch,
    SimulateMatch
}

public sealed class TournamentSessionSeed
{
    public string SessionId { get; }
    public string MissionInstanceId { get; }
    public string TownId { get; }
    public string SceneName { get; }
    public string PrizeItemId { get; }
    public string ReplacementCharacterId { get; }
    public TournamentContestantData[] FrozenContestants { get; }

    public TournamentSessionSeed(
        string sessionId,
        string missionInstanceId,
        string townId,
        string sceneName,
        string prizeItemId,
        string replacementCharacterId,
        TournamentContestantData[] frozenContestants)
    {
        SessionId = sessionId;
        MissionInstanceId = missionInstanceId;
        TownId = townId;
        SceneName = sceneName;
        PrizeItemId = prizeItemId;
        ReplacementCharacterId = replacementCharacterId;
        FrozenContestants = frozenContestants ?? new TournamentContestantData[0];
    }
}

public interface ITournamentSessionRegistry : IGameAbstraction
{
    bool HasActiveSessions { get; }
    TournamentSessionSnapshot[] GetAll();
    bool TryGet(string sessionId, out TournamentSessionSnapshot snapshot);
    bool TryGetByTown(string townId, out TournamentSessionSnapshot snapshot);
    bool CanEnterMission(string missionInstanceId, string controllerId);
    TournamentMutationStatus TryCreate(TournamentSessionSeed seed, out TournamentSessionSnapshot snapshot);
    bool IsTournamentMissionInstance(string missionInstanceId);
    TournamentMutationStatus TryJoin(
        string sessionId,
        long expectedRevision,
        string controllerId,
        string characterId,
        string displayName,
        int descriptorSeed,
        bool isLord,
        out TournamentSessionSnapshot snapshot);
    TournamentMutationStatus TryLeavePreparation(
        string sessionId,
        long expectedRevision,
        string controllerId,
        out TournamentSessionSnapshot snapshot,
        out bool sessionRemoved);
    TournamentMutationStatus TryStart(
        string sessionId,
        long expectedRevision,
        string controllerId,
        TournamentRoundData[] rounds,
        string currentMatchId,
        out TournamentSessionSnapshot snapshot);
    TournamentMutationStatus TryRequestSpectate(
        string sessionId,
        long expectedRevision,
        string controllerId,
        out TournamentSessionSnapshot snapshot);
    TournamentMutationStatus TryEnterMission(
        string sessionId,
        long expectedRevision,
        string controllerId,
        out TournamentSessionSnapshot snapshot);
    TournamentMutationStatus TryChoose(
        string sessionId,
        long expectedRevision,
        string matchId,
        string controllerId,
        TournamentPlayerChoice choice,
        out TournamentSessionSnapshot snapshot,
        out TournamentBallotOutcome outcome);
    TournamentMutationStatus TryLeaveActive(
        string sessionId,
        long expectedRevision,
        string controllerId,
        int replacementDescriptorSeed,
        string replacementDisplayName,
        out TournamentSessionSnapshot snapshot,
        out TournamentBallotOutcome outcome,
        out bool noViewers);
    TournamentMutationStatus TryBeginEmptySimulation(
        string sessionId,
        long expectedRevision,
        out TournamentSessionSnapshot snapshot);
    TournamentMutationStatus TryForceSimulation(
        string sessionId,
        long expectedRevision,
        out TournamentSessionSnapshot snapshot);
    TournamentMutationStatus TryStoreSpawnManifest(
        TournamentSpawnManifestData manifest,
        string controllerId,
        out TournamentSessionSnapshot snapshot);
    TournamentMutationStatus TryApplyMatchResult(
        TournamentMatchResultData result,
        string controllerId,
        TournamentRoundData[] rounds,
        IReadOnlyDictionary<string, int> contestantScores,
        string nextMatchId,
        string winnerSlotId,
        bool completed,
        out TournamentSessionSnapshot snapshot);
    bool TryGetSpawnManifest(string sessionId, out TournamentSpawnManifestData manifest);
    bool ApplySnapshot(TournamentSessionSnapshot snapshot);
    bool Remove(string sessionId);
}

public sealed partial class TournamentSessionRegistry : ITournamentSessionRegistry
{
    public const int MaximumCompetitorCount = 16;

    private readonly object gate = new();
    private readonly Dictionary<string, TournamentSessionState> sessionsById = new();
    private readonly Dictionary<string, string> sessionIdsByTown = new();
    private readonly HashSet<string> tournamentMissionInstanceIds = new();

    public bool IsTournamentMissionInstance(string missionInstanceId)
    {
        if (string.IsNullOrEmpty(missionInstanceId)) return false;
        lock (gate)
            return tournamentMissionInstanceIds.Contains(missionInstanceId);
    }

    public bool CanEnterMission(string missionInstanceId, string controllerId)
    {
        if (string.IsNullOrEmpty(missionInstanceId) || string.IsNullOrEmpty(controllerId)) return false;
        lock (gate)
        {
            TournamentSessionState session = sessionsById.Values
                .FirstOrDefault(state => state.MissionInstanceId == missionInstanceId);
            if (session == null || !session.IsActive) return false;
            return session.TryGetActiveContestant(controllerId, out _) ||
                session.PendingSpectators.Contains(controllerId) ||
                session.Spectators.Contains(controllerId) ||
                session.Entrants.Contains(controllerId);
        }
    }

    public bool HasActiveSessions
    {
        get
        {
            lock (gate)
            {
                return sessionsById.Values.Any(session =>
                    session.Phase != TournamentSessionPhase.Preparation &&
                    session.Phase != TournamentSessionPhase.Completed);
            }
        }
    }

    public TournamentSessionSnapshot[] GetAll()
    {
        lock (gate)
        {
            return sessionsById.Values.Select(session => session.CreateSnapshot()).ToArray();
        }
    }

    public bool TryGet(string sessionId, out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            if (sessionId != null && sessionsById.TryGetValue(sessionId, out var session))
            {
                snapshot = session.CreateSnapshot();
                return true;
            }

            snapshot = null;
            return false;
        }
    }

    public bool TryGetByTown(string townId, out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            if (townId != null &&
                sessionIdsByTown.TryGetValue(townId, out var sessionId) &&
                sessionsById.TryGetValue(sessionId, out var session))
            {
                snapshot = session.CreateSnapshot();
                return true;
            }

            snapshot = null;
            return false;
        }
    }

    public TournamentMutationStatus TryCreate(TournamentSessionSeed seed, out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            snapshot = null;
            if (seed == null ||
                string.IsNullOrEmpty(seed.SessionId) ||
                string.IsNullOrEmpty(seed.TownId) ||
                string.IsNullOrEmpty(seed.MissionInstanceId) ||
                seed.FrozenContestants.Length == 0 ||
                seed.FrozenContestants.Length > MaximumCompetitorCount)
            {
                return TournamentMutationStatus.Rejected;
            }

            if (sessionIdsByTown.TryGetValue(seed.TownId, out var existingId) &&
                sessionsById.TryGetValue(existingId, out var existing))
            {
                snapshot = existing.CreateSnapshot();
                return TournamentMutationStatus.NoChange;
            }

            if (sessionsById.ContainsKey(seed.SessionId))
                return TournamentMutationStatus.Rejected;

            var session = new TournamentSessionState(seed);
            sessionsById.Add(seed.SessionId, session);
            sessionIdsByTown.Add(seed.TownId, seed.SessionId);
            tournamentMissionInstanceIds.Add(seed.MissionInstanceId);
            snapshot = session.CreateSnapshot();
            return TournamentMutationStatus.Applied;
        }
    }

    public TournamentMutationStatus TryJoin(
        string sessionId,
        long expectedRevision,
        string controllerId,
        string characterId,
        string displayName,
        int descriptorSeed,
        bool isLord,
        out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            if (!TryResolveForMutation(sessionId, expectedRevision, out var session, out snapshot, out var status))
                return status;

            if (session.Phase != TournamentSessionPhase.Preparation)
                return TournamentMutationStatus.InvalidPhase;

            if (session.TryGetActiveContestant(controllerId, out _))
                return TournamentMutationStatus.NoChange;

            if (sessionsById.Values.Any(other => other != session && other.TryGetActiveContestant(controllerId, out _)))
                return TournamentMutationStatus.Rejected;

            var slot = session.GetReplacementSlot();
            if (slot == null)
                return TournamentMutationStatus.Full;

            descriptorSeed = TournamentDescriptorSeedAllocator.ResolveUniqueSeed(
                characterId,
                descriptorSeed,
                session.Contestants.Select(contestant => contestant.ToData()));
            slot.AssignHuman(controllerId, characterId, displayName, descriptorSeed, isLord);
            session.Revision++;
            snapshot = session.CreateSnapshot();
            return TournamentMutationStatus.Applied;
        }
    }

    public TournamentMutationStatus TryLeavePreparation(
        string sessionId,
        long expectedRevision,
        string controllerId,
        out TournamentSessionSnapshot snapshot,
        out bool sessionRemoved)
    {
        lock (gate)
        {
            sessionRemoved = false;
            if (!TryResolveForMutation(sessionId, expectedRevision, out var session, out snapshot, out var status))
                return status;

            if (session.Phase != TournamentSessionPhase.Preparation)
                return TournamentMutationStatus.InvalidPhase;

            if (!session.TryGetActiveContestant(controllerId, out var slot))
                return TournamentMutationStatus.NoChange;

            slot.RestoreFrozenNpc();
            session.Revision++;

            if (!session.Contestants.Any(contestant => contestant.IsHuman))
            {
                RemoveSession(session);
                snapshot = null;
                sessionRemoved = true;
                return TournamentMutationStatus.Applied;
            }

            snapshot = session.CreateSnapshot();
            return TournamentMutationStatus.Applied;
        }
    }

    public TournamentMutationStatus TryStart(
        string sessionId,
        long expectedRevision,
        string controllerId,
        TournamentRoundData[] rounds,
        string currentMatchId,
        out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            if (!TryResolveForMutation(sessionId, expectedRevision, out var session, out snapshot, out var status))
                return status;

            if (session.Phase != TournamentSessionPhase.Preparation)
                return TournamentMutationStatus.InvalidPhase;

            if (!session.TryGetActiveContestant(controllerId, out _))
                return TournamentMutationStatus.NotParticipant;

            if (rounds == null || rounds.Length == 0 || string.IsNullOrEmpty(currentMatchId))
                return TournamentMutationStatus.Rejected;

            session.Rounds = rounds;
            session.CurrentMatchId = currentMatchId;
            session.Phase = TournamentSessionPhase.AwaitingChoices;
            session.BracketRevision++;
            session.Choices.Clear();
            session.Revision++;
            snapshot = session.CreateSnapshot();
            return TournamentMutationStatus.Applied;
        }
    }

    public TournamentMutationStatus TryRequestSpectate(
        string sessionId,
        long expectedRevision,
        string controllerId,
        out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            if (!TryResolveForMutation(sessionId, expectedRevision, out var session, out snapshot, out var status))
                return status;

            if (!session.IsActive)
                return TournamentMutationStatus.InvalidPhase;

            if (session.TryGetActiveContestant(controllerId, out _) || session.Spectators.Contains(controllerId))
                return TournamentMutationStatus.NoChange;

            if (!session.PendingSpectators.Add(controllerId))
                return TournamentMutationStatus.NoChange;

            session.Spectators.Add(controllerId);
            session.Revision++;
            snapshot = session.CreateSnapshot();
            return TournamentMutationStatus.Applied;
        }
    }

    public TournamentMutationStatus TryEnterMission(
        string sessionId,
        long expectedRevision,
        string controllerId,
        out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
            return EnterMission(sessionId, expectedRevision, controllerId, out snapshot);
    }

    private TournamentMutationStatus EnterMission(
        string sessionId,
        long expectedRevision,
        string controllerId,
        out TournamentSessionSnapshot snapshot)
    {
        if (!TryResolveForMutation(sessionId, expectedRevision, out var session, out snapshot, out var status) &&
            status != TournamentMutationStatus.StaleRevision)
            return status;
        if (!session.IsActive)
            return TournamentMutationStatus.InvalidPhase;

        bool isCompetitor = session.TryGetActiveContestant(controllerId, out _);
        if (!isCompetitor && !session.PendingSpectators.Remove(controllerId) && !session.Spectators.Contains(controllerId))
            return TournamentMutationStatus.NotParticipant;

        bool changed = !isCompetitor && session.Spectators.Add(controllerId);
        if (!session.Entrants.Contains(controllerId))
        {
            session.Entrants.Add(controllerId);
            changed = true;
        }
        if (!changed)
            return TournamentMutationStatus.NoChange;

        session.Revision++;
        snapshot = session.CreateSnapshot();
        return TournamentMutationStatus.Applied;
    }
    public TournamentMutationStatus TryChoose(
        string sessionId,
        long expectedRevision,
        string matchId,
        string controllerId,
        TournamentPlayerChoice choice,
        out TournamentSessionSnapshot snapshot,
        out TournamentBallotOutcome outcome)
    {
        lock (gate)
            return Choose(sessionId, expectedRevision, matchId, controllerId, choice, out snapshot, out outcome);
    }

    private TournamentMutationStatus Choose(
        string sessionId,
        long expectedRevision,
        string matchId,
        string controllerId,
        TournamentPlayerChoice choice,
        out TournamentSessionSnapshot snapshot,
        out TournamentBallotOutcome outcome)
    {
        outcome = TournamentBallotOutcome.Open;
        if (!TryResolveForMutation(sessionId, expectedRevision, out var session, out snapshot, out var status) &&
            status != TournamentMutationStatus.StaleRevision)
            return status;
        if (session.Phase != TournamentSessionPhase.AwaitingChoices || session.CurrentMatchId != matchId)
            return TournamentMutationStatus.InvalidPhase;
        if (!session.IsVoter(controllerId))
            return TournamentMutationStatus.NotParticipant;
        if (!IsValidChoice(session, controllerId, choice))
            return TournamentMutationStatus.InvalidChoice;
        if (session.Choices.TryGetValue(controllerId, out var existing) && existing == choice)
            return TournamentMutationStatus.NoChange;

        session.Choices[controllerId] = choice;
        outcome = session.GetBallotOutcome();
        session.ApplyBallotOutcome(outcome);
        session.Revision++;
        snapshot = session.CreateSnapshot();
        return TournamentMutationStatus.Applied;
    }

    private static bool IsValidChoice(
        TournamentSessionState session,
        string controllerId,
        TournamentPlayerChoice choice)
    {
        bool isCurrentCompetitor = session.IsControllerInCurrentMatch(controllerId);
        if (choice == TournamentPlayerChoice.Join)
            return isCurrentCompetitor;
        if (choice == TournamentPlayerChoice.Watch)
            return !isCurrentCompetitor;
        if (choice == TournamentPlayerChoice.Skip)
            return session.IsSkipAllowed();
        return false;
    }
    public TournamentMutationStatus TryLeaveActive(
        string sessionId,
        long expectedRevision,
        string controllerId,
        int replacementDescriptorSeed,
        string replacementDisplayName,
        out TournamentSessionSnapshot snapshot,
        out TournamentBallotOutcome outcome,
        out bool noViewers)
    {
        lock (gate)
        {
            outcome = TournamentBallotOutcome.Open;
            noViewers = false;
            if (!TryResolveForMutation(sessionId, expectedRevision, out var session, out snapshot, out var status))
                return status;

            if (!session.IsActive)
                return TournamentMutationStatus.InvalidPhase;

            bool changed = false;
            if (session.TryGetActiveContestant(controllerId, out var slot))
            {
                replacementDescriptorSeed = TournamentDescriptorSeedAllocator.ResolveUniqueSeed(
                    session.ReplacementCharacterId,
                    replacementDescriptorSeed,
                    session.Contestants.Select(contestant => contestant.ToData()));
                slot.ReplaceDepartedHuman(
                    session.ReplacementCharacterId,
                    replacementDisplayName,
                    replacementDescriptorSeed);
                session.DepartedControllers.Add(controllerId);
                if (session.Phase == TournamentSessionPhase.AwaitingChoices)
                    session.BracketRevision++;
                changed = true;
            }

            changed |= session.Spectators.Remove(controllerId);
            changed |= session.PendingSpectators.Remove(controllerId);
            changed |= session.Entrants.Remove(controllerId);
            changed |= session.Choices.Remove(controllerId);

            if (!changed)
                return TournamentMutationStatus.NoChange;

            if (session.Phase == TournamentSessionPhase.AwaitingChoices)
            {
                outcome = session.GetBallotOutcome();
                session.ApplyBallotOutcome(outcome);
            }

            noViewers = !session.Contestants.Any(contestant => contestant.IsHuman) && session.Spectators.Count == 0;
            session.Revision++;
            snapshot = session.CreateSnapshot();
            return TournamentMutationStatus.Applied;
        }
    }

    public TournamentMutationStatus TryStoreSpawnManifest(
        TournamentSpawnManifestData manifest,
        string controllerId,
        out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            snapshot = null;
            if (manifest == null || !sessionsById.TryGetValue(manifest.SessionId, out var session))
                return TournamentMutationStatus.NotFound;

            snapshot = session.CreateSnapshot();
            if (manifest.Revision != session.Revision ||
                manifest.BracketRevision != session.BracketRevision)
            {
                return manifest.Revision < session.Revision || manifest.BracketRevision < session.BracketRevision
                    ? TournamentMutationStatus.StaleRevision
                    : TournamentMutationStatus.Rejected;
            }
            if (session.Phase != TournamentSessionPhase.LiveMatch ||
                manifest.MatchId != session.CurrentMatchId)
                return TournamentMutationStatus.InvalidPhase;

            if (session.HostControllerId != controllerId)
                return TournamentMutationStatus.Rejected;
            if (!TournamentSpawnManifestValidator.IsValid(manifest, snapshot))
                return TournamentMutationStatus.Rejected;
            if (manifest.Sequence <= session.LastManifestSequence)
                return TournamentMutationStatus.NoChange;

            session.LastManifestSequence = manifest.Sequence;
            session.SpawnManifest = manifest;
            return TournamentMutationStatus.Applied;
        }
    }

    public TournamentMutationStatus TryApplyMatchResult(
        TournamentMatchResultData result,
        string controllerId,
        TournamentRoundData[] rounds,
        IReadOnlyDictionary<string, int> contestantScores,
        string nextMatchId,
        string winnerSlotId,
        bool completed,
        out TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            snapshot = null;
            if (result == null || !sessionsById.TryGetValue(result.SessionId, out var session))
                return TournamentMutationStatus.NotFound;

            snapshot = session.CreateSnapshot();
            TournamentMutationStatus status = ValidateMatchResult(
                result,
                controllerId,
                contestantScores,
                session);
            if (status != TournamentMutationStatus.Applied)
                return status;

            ApplyMatchResult(
                session,
                result,
                rounds,
                contestantScores,
                nextMatchId,
                winnerSlotId,
                completed);
            snapshot = session.CreateSnapshot();
            return TournamentMutationStatus.Applied;
        }
    }

    private static TournamentMutationStatus ValidateMatchResult(
        TournamentMatchResultData result,
        string controllerId,
        IReadOnlyDictionary<string, int> contestantScores,
        TournamentSessionState session)
    {
        if (result.Revision != session.Revision || result.BracketRevision != session.BracketRevision)
        {
            return result.Revision < session.Revision || result.BracketRevision < session.BracketRevision
                ? TournamentMutationStatus.StaleRevision
                : TournamentMutationStatus.Rejected;
        }
        if ((session.Phase != TournamentSessionPhase.LiveMatch &&
             session.Phase != TournamentSessionPhase.SimulatingMatch) ||
            result.MatchId != session.CurrentMatchId)
            return TournamentMutationStatus.InvalidPhase;
        if (session.Phase == TournamentSessionPhase.LiveMatch && session.HostControllerId != controllerId)
            return TournamentMutationStatus.Rejected;
        if (result.Sequence <= session.LastResultSequence)
            return TournamentMutationStatus.NoChange;
        return HasValidContestantScores(session, contestantScores)
            ? TournamentMutationStatus.Applied
            : TournamentMutationStatus.Rejected;
    }

    private static bool HasValidContestantScores(
        TournamentSessionState session,
        IReadOnlyDictionary<string, int> contestantScores)
    {
        if (contestantScores == null || contestantScores.Count != session.Contestants.Count)
            return false;
        return session.Contestants.All(contestant =>
            contestantScores.TryGetValue(contestant.SlotId, out var score) && score >= 0);
    }

    private static void ApplyMatchResult(
        TournamentSessionState session,
        TournamentMatchResultData result,
        TournamentRoundData[] rounds,
        IReadOnlyDictionary<string, int> contestantScores,
        string nextMatchId,
        string winnerSlotId,
        bool completed)
    {
        foreach (TournamentContestantSlot contestant in session.Contestants)
            contestant.Score = contestantScores[contestant.SlotId];

        session.LastResultSequence = result.Sequence;
        session.SpawnManifest = null;
        session.LastManifestSequence = 0;
        session.Rounds = rounds ?? session.Rounds;
        session.BracketRevision++;
        session.Choices.Clear();
        session.WinnerSlotId = winnerSlotId;
        session.CurrentMatchId = completed ? null : nextMatchId;
        session.Phase = completed
            ? TournamentSessionPhase.Completed
            : TournamentSessionPhase.AwaitingChoices;

        // Sequence numbers are scoped to the current match and submitting authority. The next host or
        // server simulation begins its own sequence at one; exact match/revision checks reject old results.
        session.LastResultSequence = 0;
        session.Revision++;
    }
    public bool TryGetSpawnManifest(string sessionId, out TournamentSpawnManifestData manifest)
    {
        lock (gate)
        {
            if (sessionId != null && sessionsById.TryGetValue(sessionId, out var session))
            {
                manifest = session.SpawnManifest;
                return manifest != null;
            }

            manifest = null;
            return false;
        }
    }

    public bool ApplySnapshot(TournamentSessionSnapshot snapshot)
    {
        lock (gate)
        {
            if (snapshot == null ||
                string.IsNullOrEmpty(snapshot.SessionId) ||
                string.IsNullOrEmpty(snapshot.MissionInstanceId) ||
                string.IsNullOrEmpty(snapshot.TownId))
                return false;

            tournamentMissionInstanceIds.Add(snapshot.MissionInstanceId);

            if (sessionsById.TryGetValue(snapshot.SessionId, out var existing) && existing.Revision >= snapshot.Revision)
                return false;

            if (sessionIdsByTown.TryGetValue(snapshot.TownId, out var oldSessionId) && oldSessionId != snapshot.SessionId)
                sessionsById.Remove(oldSessionId);

            var session = new TournamentSessionState(snapshot);
            sessionsById[snapshot.SessionId] = session;
            sessionIdsByTown[snapshot.TownId] = snapshot.SessionId;
            return true;
        }
    }

    public bool Remove(string sessionId)
    {
        lock (gate)
        {
            if (sessionId == null || !sessionsById.TryGetValue(sessionId, out var session))
                return false;

            RemoveSession(session);
            return true;
        }
    }

    private bool TryResolveForMutation(
        string sessionId,
        long expectedRevision,
        out TournamentSessionState session,
        out TournamentSessionSnapshot snapshot,
        out TournamentMutationStatus status)
    {
        session = null;
        if (sessionId == null || !sessionsById.TryGetValue(sessionId, out var resolvedSession))
        {
            snapshot = null;
            status = TournamentMutationStatus.NotFound;
            return false;
        }
        session = resolvedSession;

        snapshot = session.CreateSnapshot();
        if (session.Revision != expectedRevision)
        {
            status = expectedRevision < session.Revision
                ? TournamentMutationStatus.StaleRevision
                : TournamentMutationStatus.Rejected;
            return false;
        }

        status = TournamentMutationStatus.Applied;
        return true;
    }

    private void RemoveSession(TournamentSessionState session)
    {
        sessionsById.Remove(session.SessionId);
        sessionIdsByTown.Remove(session.TownId);
    }

    private sealed class TournamentSessionState
    {
        public readonly string SessionId;
        public readonly string MissionInstanceId;
        public readonly string TownId;
        public readonly string SceneName;
        public readonly string PrizeItemId;
        public readonly string ReplacementCharacterId;
        public readonly List<TournamentContestantSlot> Contestants;
        public readonly HashSet<string> Spectators = new();
        public readonly HashSet<string> PendingSpectators = new();
        public readonly HashSet<string> DepartedControllers = new();
        public readonly List<string> Entrants = new();
        public readonly Dictionary<string, TournamentPlayerChoice> Choices = new();

        public TournamentSessionPhase Phase;
        public long Revision;
        public long BracketRevision;
        public string CurrentMatchId;
        public TournamentRoundData[] Rounds;
        public string WinnerSlotId;
        public TournamentSpawnManifestData SpawnManifest;
        public long LastManifestSequence;
        public long LastResultSequence;

        public string HostControllerId => Entrants.FirstOrDefault();
        public bool IsActive => Phase != TournamentSessionPhase.Preparation && Phase != TournamentSessionPhase.Completed;

        public TournamentSessionState(TournamentSessionSeed seed)
        {
            SessionId = seed.SessionId;
            MissionInstanceId = seed.MissionInstanceId;
            TownId = seed.TownId;
            SceneName = seed.SceneName;
            PrizeItemId = seed.PrizeItemId;
            ReplacementCharacterId = seed.ReplacementCharacterId;
            Contestants = seed.FrozenContestants.Select(contestant => new TournamentContestantSlot(contestant)).ToList();
            Phase = TournamentSessionPhase.Preparation;
            Rounds = new TournamentRoundData[0];
        }

        public TournamentSessionState(TournamentSessionSnapshot snapshot)
        {
            SessionId = snapshot.SessionId;
            MissionInstanceId = snapshot.MissionInstanceId;
            TownId = snapshot.TownId;
            SceneName = snapshot.SceneName;
            PrizeItemId = snapshot.PrizeItemId;
            ReplacementCharacterId = null;
            Contestants = snapshot.Contestants.Select(contestant => new TournamentContestantSlot(contestant)).ToList();
            Phase = snapshot.Phase;
            Revision = snapshot.Revision;
            BracketRevision = snapshot.BracketRevision;
            CurrentMatchId = snapshot.CurrentMatchId;
            Rounds = snapshot.Rounds ?? new TournamentRoundData[0];
            WinnerSlotId = snapshot.WinnerSlotId;
            Spectators.UnionWith(snapshot.SpectatorControllerIds ?? new string[0]);
            Choices = (snapshot.Choices ?? new TournamentPlayerChoiceData[0])
                .ToDictionary(choice => choice.ControllerId, choice => choice.Choice);

            if (snapshot.HostControllerId != null)
                Entrants.Add(snapshot.HostControllerId);
            Entrants.AddRange(snapshot.SuccessorControllerIds ?? new string[0]);
        }

        public TournamentContestantSlot GetReplacementSlot()
        {
            return Contestants.LastOrDefault(contestant => !contestant.IsHuman && !contestant.IsReplaced && !contestant.IsLord)
                ?? Contestants.LastOrDefault(contestant => !contestant.IsHuman && !contestant.IsReplaced);
        }

        public bool TryGetActiveContestant(string controllerId, out TournamentContestantSlot contestant)
        {
            contestant = Contestants.FirstOrDefault(slot => slot.IsHuman && slot.ControllerId == controllerId);
            return contestant != null;
        }

        public bool IsVoter(string controllerId)
        {
            return TryGetActiveContestant(controllerId, out _) || Spectators.Contains(controllerId);
        }

        public bool IsControllerInCurrentMatch(string controllerId)
        {
            if (!TryGetActiveContestant(controllerId, out var contestant))
                return false;

            return GetCurrentMatchSlotIds().Contains(contestant.SlotId);
        }

        public bool IsSkipAllowed()
        {
            var currentSlots = GetCurrentMatchSlotIds();
            return !Contestants.Any(contestant => contestant.IsHuman && currentSlots.Contains(contestant.SlotId));
        }

        public TournamentBallotOutcome GetBallotOutcome()
        {
            var voters = Contestants
                .Where(contestant => contestant.IsHuman)
                .Select(contestant => contestant.ControllerId)
                .Concat(Spectators)
                .Distinct()
                .ToArray();

            if (voters.Length == 0)
                return TournamentBallotOutcome.SimulateMatch;

            bool allReady = voters.All(controllerId =>
                Choices.TryGetValue(controllerId, out var choice) &&
                (choice == TournamentPlayerChoice.Join || choice == TournamentPlayerChoice.Watch));
            if (allReady)
                return TournamentBallotOutcome.StartLiveMatch;

            bool allSkip = IsSkipAllowed() && voters.All(controllerId =>
                Choices.TryGetValue(controllerId, out var choice) && choice == TournamentPlayerChoice.Skip);
            return allSkip ? TournamentBallotOutcome.SimulateMatch : TournamentBallotOutcome.Open;
        }

        public void ApplyBallotOutcome(TournamentBallotOutcome outcome)
        {
            if (outcome == TournamentBallotOutcome.StartLiveMatch)
                Phase = TournamentSessionPhase.LiveMatch;
            else if (outcome == TournamentBallotOutcome.SimulateMatch)
                Phase = TournamentSessionPhase.SimulatingMatch;
        }

        public TournamentSessionSnapshot CreateSnapshot()
        {
            var voters = Contestants
                .Where(contestant => contestant.IsHuman)
                .Select(contestant => contestant.ControllerId)
                .Concat(Spectators)
                .Distinct()
                .ToArray();
            int ready = voters.Count(controllerId =>
                Choices.TryGetValue(controllerId, out var choice) &&
                (choice == TournamentPlayerChoice.Join || choice == TournamentPlayerChoice.Watch));
            int skip = voters.Count(controllerId =>
                Choices.TryGetValue(controllerId, out var choice) && choice == TournamentPlayerChoice.Skip);

            return new TournamentSessionSnapshot(
                SessionId,
                MissionInstanceId,
                TownId,
                SceneName,
                PrizeItemId,
                Phase,
                Revision,
                BracketRevision,
                CurrentMatchId,
                HostControllerId,
                Entrants.Skip(1).ToArray(),
                Contestants.Select(contestant => contestant.ToData()).ToArray(),
                Spectators.ToArray(),
                Choices.Select(choice => new TournamentPlayerChoiceData(choice.Key, choice.Value)).ToArray(),
                Rounds,
                ready,
                skip,
                voters.Length,
                IsSkipAllowed(),
                Phase == TournamentSessionPhase.Completed,
                WinnerSlotId);
        }

        private HashSet<string> GetCurrentMatchSlotIds()
        {
            var match = Rounds
                .SelectMany(round => round.Matches ?? new TournamentMatchData[0])
                .FirstOrDefault(candidate => candidate.MatchId == CurrentMatchId);
            if (match == null)
                return new HashSet<string>();

            return new HashSet<string>(match.Teams.SelectMany(team => team.ParticipantSlotIds));
        }
    }

    private sealed class TournamentContestantSlot
    {
        public readonly string SlotId;
        public readonly string FrozenCharacterId;
        public readonly int FrozenDescriptorSeed;
        public readonly string FrozenDisplayName;
        public readonly bool FrozenIsLord;

        public string CharacterId;
        public int DescriptorSeed;
        public string ControllerId;
        public string DisplayName;
        public bool IsHuman;
        public bool IsReplaced;
        public bool IsLord;
        public string DisplacedCharacterId;
        public int Score;

        public TournamentContestantSlot(TournamentContestantData contestant)
        {
            SlotId = contestant.SlotId;
            FrozenCharacterId = contestant.CharacterId;
            FrozenDescriptorSeed = contestant.DescriptorSeed;
            FrozenDisplayName = contestant.DisplayName;
            FrozenIsLord = contestant.IsLord;
            CharacterId = contestant.CharacterId;
            DescriptorSeed = contestant.DescriptorSeed;
            ControllerId = contestant.ControllerId;
            DisplayName = contestant.DisplayName;
            IsHuman = contestant.IsHuman;
            IsReplaced = contestant.IsReplaced;
            IsLord = contestant.IsLord;
            DisplacedCharacterId = contestant.DisplacedCharacterId;
            Score = contestant.Score;
        }

        public void AssignHuman(
            string controllerId,
            string characterId,
            string displayName,
            int descriptorSeed,
            bool isLord)
        {
            DisplacedCharacterId = CharacterId;
            CharacterId = characterId;
            DescriptorSeed = descriptorSeed;
            ControllerId = controllerId;
            DisplayName = displayName;
            IsHuman = true;
            IsReplaced = false;
            IsLord = isLord;
            Score = 0;
        }

        public void RestoreFrozenNpc()
        {
            CharacterId = FrozenCharacterId;
            DescriptorSeed = FrozenDescriptorSeed;
            ControllerId = null;
            DisplayName = FrozenDisplayName;
            IsHuman = false;
            IsReplaced = false;
            IsLord = FrozenIsLord;
            DisplacedCharacterId = null;
            Score = 0;
        }

        public void ReplaceDepartedHuman(string characterId, string displayName, int descriptorSeed)
        {
            var departedCharacterId = CharacterId;
            CharacterId = characterId;
            DescriptorSeed = descriptorSeed;
            ControllerId = null;
            DisplayName = displayName;
            IsHuman = false;
            IsReplaced = true;
            IsLord = false;
            DisplacedCharacterId = departedCharacterId;
            Score = 0;
        }

        public TournamentContestantData ToData()
        {
            return new TournamentContestantData(
                SlotId,
                CharacterId,
                DescriptorSeed,
                ControllerId,
                DisplayName,
                IsHuman,
                IsReplaced,
                IsLord,
                DisplacedCharacterId,
                Score);
        }
    }
}
