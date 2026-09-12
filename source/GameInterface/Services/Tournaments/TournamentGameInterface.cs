using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Tournaments.Data;
using SandBox.Tournaments.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Tournaments;

public sealed class TournamentBracketUpdate
{
    public TournamentRoundData[] Rounds { get; }
    public string CurrentMatchId { get; }
    public string WinnerSlotId { get; }
    public bool IsCompleted { get; }
    public IReadOnlyDictionary<string, int> ContestantScores { get; }
    public string[] MatchWinnerSlotIds { get; }

    public TournamentBracketUpdate(
        TournamentRoundData[] rounds,
        string currentMatchId,
        string winnerSlotId,
        bool isCompleted,
        IReadOnlyDictionary<string, int> contestantScores,
        string[] matchWinnerSlotIds)
    {
        Rounds = rounds ?? Array.Empty<TournamentRoundData>();
        CurrentMatchId = currentMatchId;
        WinnerSlotId = winnerSlotId;
        IsCompleted = isCompleted;
        ContestantScores = contestantScores ?? new Dictionary<string, int>();
        MatchWinnerSlotIds = matchWinnerSlotIds ?? Array.Empty<string>();
    }
}

public interface ITournamentGameInterface : IGameAbstraction
{
    bool TryFreezeTournament(Town town, FightTournamentGame tournamentGame, out TournamentSessionSeed seed);
    bool TryCreateBracket(TournamentSessionSnapshot snapshot, out TournamentBracketUpdate bracket);
    bool TryAdvanceBracket(
        TournamentSessionSnapshot snapshot,
        TournamentMatchResultData result,
        out TournamentBracketUpdate bracket);
    bool TryRehydrateGame(TournamentSessionSnapshot snapshot, out FightTournamentGame tournamentGame);
    bool TryApplyLockedPrize(TournamentSessionSnapshot snapshot);
    bool TrySimulateCurrentMatchUnbiased(
        TournamentSessionSnapshot snapshot,
        long sequence,
        out TournamentMatchResultData result);
    bool TryGetBetQuote(TournamentSessionSnapshot snapshot, Hero hero, string slotId, out TournamentBetQuote quote);
}

public sealed partial class TournamentGameInterface : ITournamentGameInterface
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public TournamentGameInterface(IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    public bool TryFreezeTournament(Town town, FightTournamentGame tournamentGame, out TournamentSessionSeed seed)
    {
        seed = null;
        if (!TryResolveFreezeContext(town, tournamentGame, out var townId, out var sceneName))
            return false;
        if (!TryCreateFrozenRoster(town, tournamentGame, out var sortedCharacters))
            return false;
        if (!TryResolveFrozenRewards(
                town,
                tournamentGame,
                sortedCharacters,
                out var prizeId,
                out var replacementId))
            return false;

        return TryCreateTournamentSeed(
            townId,
            sceneName,
            prizeId,
            replacementId,
            sortedCharacters,
            out seed);
    }

    private bool TryResolveFreezeContext(
        Town town,
        FightTournamentGame tournamentGame,
        out string townId,
        out string sceneName)
    {
        townId = null;
        sceneName = null;
        if (town?.Settlement == null || tournamentGame == null || tournamentGame.Town != town)
            return false;
        if (tournamentGame.GetType() != typeof(FightTournamentGame))
            return false;
        if (!objectManager.TryGetId(town, out townId))
            return false;

        sceneName = town.Settlement.LocationComplex?.GetScene("arena", town.GetWallLevel());
        return !string.IsNullOrEmpty(sceneName);
    }

    private bool TryResolveFrozenRewards(
        Town town,
        FightTournamentGame tournamentGame,
        MBList<CharacterObject> sortedCharacters,
        out string prizeId,
        out string replacementId)
    {
        prizeId = null;
        replacementId = null;
        CharacterObject replacement = town.Culture?.BasicTroop;
        if (replacement == null || !objectManager.TryGetId(replacement, out replacementId))
            return false;

        ItemObject prize = LockPrizeForFrozenRoster(town, tournamentGame, sortedCharacters);
        return prize != null && objectManager.TryGetId(prize, out prizeId);
    }

    private bool TryCreateTournamentSeed(
        string townId,
        string sceneName,
        string prizeId,
        string replacementId,
        IReadOnlyList<CharacterObject> sortedCharacters,
        out TournamentSessionSeed seed)
    {
        seed = null;
        if (!TryCreateSessionId(objectManager, out var sessionId))
            return false;
        if (!TryCreateFrozenContestants(sortedCharacters, sessionId, out var contestants))
            return false;

        seed = new TournamentSessionSeed(
            sessionId,
            sessionId,
            townId,
            sceneName,
            prizeId,
            replacementId,
            contestants);
        return true;
    }

    internal static bool TryCreateSessionId(IObjectManager objectManager, out string sessionId)
    {
        var identity = new TournamentSessionIdentity();
        if (!objectManager.AddNewObject(identity, out sessionId))
            return false;

        return objectManager.Remove(identity);
    }

    private bool TryCreateFrozenRoster(
        Town town,
        FightTournamentGame tournamentGame,
        out MBList<CharacterObject> sortedCharacters)
    {
        var playerCharacters = ResolvePlayerCharacters();
        var rosterGame = ObjectHelper.SkipConstructor<PlayerFilteredFightTournamentGame>();
        rosterGame.Town = town;
        rosterGame.Mode = tournamentGame.Mode;
        rosterGame.ExcludedCharacters = playerCharacters;
        var frozenCharacters = rosterGame
            .GetParticipantCharacters(town.Settlement, false)
            .Where(character => character != null && !playerCharacters.Contains(character))
            .ToList();
        if (frozenCharacters.Count != tournamentGame.MaximumParticipantCount)
        {
            sortedCharacters = null;
            return false;
        }

        sortedCharacters = new MBList<CharacterObject>(frozenCharacters);
        tournamentGame.SortTournamentParticipants(sortedCharacters);
        return true;
    }

    private bool TryCreateFrozenContestants(
        IReadOnlyList<CharacterObject> sortedCharacters,
        string sessionId,
        out TournamentContestantData[] contestants)
    {
        contestants = new TournamentContestantData[sortedCharacters.Count];
        for (int i = 0; i < sortedCharacters.Count; i++)
        {
            CharacterObject character = sortedCharacters[i];
            if (!objectManager.TryGetId(character, out var characterId))
                return false;

            bool isLord = character.IsHero && character.HeroObject?.IsLord == true;
            int descriptorSeed = TournamentDescriptorSeedAllocator.ResolveUniqueSeed(
                characterId,
                MBRandom.RandomInt(int.MaxValue),
                contestants.Take(i));
            contestants[i] = new TournamentContestantData(
                $"{sessionId}:slot:{i}",
                characterId,
                descriptorSeed,
                null,
                character.Name?.ToString() ?? characterId,
                false,
                false,
                isLord,
                null,
                0);
        }
        return true;
    }
    public bool TryCreateBracket(TournamentSessionSnapshot snapshot, out TournamentBracketUpdate bracket)
    {
        bracket = null;
        if (!TryRehydrateGame(snapshot, out var tournamentGame) ||
            !TryCreateParticipants(snapshot, out var participants, out var slotIds))
        {
            return false;
        }

        var behavior = ObjectHelper.SkipConstructor<TournamentBehavior>();
        behavior._tournamentGame = tournamentGame;
        behavior.Rounds = new TournamentRound[4];
        behavior._participants = participants.Values.ToArray();
        Shuffle(behavior._participants, upperBound => MBRandom.RandomInt(upperBound));
        behavior.CurrentRoundIndex = 0;
        behavior.LastMatch = null;
        behavior.Winner = null;
        behavior.CreateTournamentTree();
        behavior.FillParticipants(behavior._participants.ToList());

        TournamentRoundData[] rounds = PackRounds(behavior.Rounds, slotIds);
        string currentMatchId = FindCurrentMatchId(rounds);
        bracket = new TournamentBracketUpdate(
            rounds,
            currentMatchId,
            null,
            false,
            participants.ToDictionary(pair => pair.Key, pair => pair.Value.Score),
            Array.Empty<string>());
        return currentMatchId != null;
    }

    internal static void Shuffle<T>(IList<T> values, Func<int, int> nextIndex)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = nextIndex(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    public bool TryAdvanceBracket(
        TournamentSessionSnapshot snapshot,
        TournamentMatchResultData result,
        out TournamentBracketUpdate bracket)
    {
        bracket = null;
        if (snapshot == null || result == null || snapshot.CurrentMatchId != result.MatchId)
            return false;
        if (!TryHydrateRounds(snapshot, out var rounds, out var participants, out var slotIds))
            return false;

        if (!TryFindCurrentMatch(snapshot, rounds, out var roundIndex, out var match))
            return false;

        TournamentMatchData matchData = snapshot.Rounds[roundIndex].Matches
            .FirstOrDefault(candidate => candidate.MatchId == snapshot.CurrentMatchId);
        if (matchData == null || !TryApplyScores(match, matchData, result.TeamScores))
            return false;
        rounds[roundIndex].EndMatch();

        var winners = match.Winners.ToArray();
        if (roundIndex + 1 < rounds.Length)
            AddWinnersToNextRound(rounds[roundIndex + 1], winners);

        TournamentRoundData[] packedRounds = PackRounds(rounds, slotIds);
        string currentMatchId = FindCurrentMatchId(packedRounds);
        bool completed = currentMatchId == null;
        string[] matchWinnerSlotIds = winners
            .Where(slotIds.ContainsKey)
            .Select(winner => slotIds[winner])
            .ToArray();
        string winnerSlotId = completed && winners.Length > 0 && slotIds.TryGetValue(winners[0], out var slotId)
            ? slotId
            : null;
        bracket = new TournamentBracketUpdate(
            packedRounds,
            currentMatchId,
            winnerSlotId,
            completed,
            participants.ToDictionary(pair => pair.Key, pair => pair.Value.Score),
            matchWinnerSlotIds);
        return true;
    }

    internal static void AddWinnersToNextRound(
        TournamentRound nextRound,
        IEnumerable<TournamentParticipant> winners)
    {
        foreach (TournamentParticipant winner in winners)
        {
            winner.IsAssigned = false;
            nextRound.AddParticipant(winner, false);
        }
    }

    public bool TryApplyLockedPrize(TournamentSessionSnapshot snapshot)
    {
        if (snapshot == null ||
            !objectManager.TryGetObject(snapshot.TownId, out Town town) ||
            !objectManager.TryGetObject(snapshot.PrizeItemId, out ItemObject prize) ||
            Campaign.Current?.TournamentManager?.GetTournamentGame(town) is not FightTournamentGame game ||
            game.GetType() != typeof(FightTournamentGame))
        {
            return false;
        }

        game.Prize = prize;
        game._lastRecordedLordCountForTournamentPrize = snapshot.Contestants.Count(contestant => contestant.IsLord);
        return true;
    }

    public bool TryRehydrateGame(TournamentSessionSnapshot snapshot, out FightTournamentGame tournamentGame)
    {
        tournamentGame = null;
        if (snapshot == null || !objectManager.TryGetObject(snapshot.TownId, out Town town))
            return false;

        ItemObject prize = null;
        if (snapshot.PrizeItemId != null && !objectManager.TryGetObject(snapshot.PrizeItemId, out prize))
            return false;

        tournamentGame = ObjectHelper.SkipConstructor<FightTournamentGame>();
        tournamentGame.Town = town;
        tournamentGame.CreationTime = CampaignTime.Now;
        tournamentGame.Mode = TournamentGame.QualificationMode.TeamScore;
        tournamentGame.Prize = prize;
        tournamentGame._lastRecordedLordCountForTournamentPrize = snapshot.Contestants.Count(contestant => contestant.IsLord);
        return true;
    }

    private HashSet<CharacterObject> ResolvePlayerCharacters()
    {
        var characters = new HashSet<CharacterObject>();
        foreach (var player in playerManager.Players)
        {
            if (objectManager.TryGetObject(player.HeroId, out Hero hero) && hero?.CharacterObject != null)
                characters.Add(hero.CharacterObject);
        }
        return characters;
    }

    internal static ItemObject LockPrizeForFrozenRoster(
        Town town,
        FightTournamentGame tournamentGame,
        MBList<CharacterObject> frozenCharacters)
    {
        var prizeGame = ObjectHelper.SkipConstructor<FrozenRosterFightTournamentGame>();
        prizeGame.Town = town;
        prizeGame.Mode = tournamentGame.Mode;
        prizeGame.Prize = tournamentGame.Prize;
        prizeGame._lastRecordedLordCountForTournamentPrize =
            tournamentGame._lastRecordedLordCountForTournamentPrize;
        prizeGame.FrozenCharacters = frozenCharacters;
        ItemObject prize = prizeGame.GetTournamentPrize(
            false,
            tournamentGame._lastRecordedLordCountForTournamentPrize);
        tournamentGame.Prize = prize;
        tournamentGame._lastRecordedLordCountForTournamentPrize =
            prizeGame._lastRecordedLordCountForTournamentPrize;
        return prize;
    }

    private bool TryCreateParticipants(
        TournamentSessionSnapshot snapshot,
        out Dictionary<string, TournamentParticipant> participants,
        out Dictionary<TournamentParticipant, string> slotIds)
    {
        participants = new Dictionary<string, TournamentParticipant>();
        slotIds = new Dictionary<TournamentParticipant, string>();
        foreach (var contestant in snapshot.Contestants)
        {
            if (!objectManager.TryGetObject(contestant.CharacterId, out CharacterObject character))
                return false;

            var participant = new TournamentParticipant(
                character,
                new UniqueTroopDescriptor(contestant.DescriptorSeed));
            participant.Score = contestant.Score;
            participants.Add(contestant.SlotId, participant);
            slotIds.Add(participant, contestant.SlotId);
        }
        return true;
    }

    private bool TryHydrateRounds(
        TournamentSessionSnapshot snapshot,
        out TournamentRound[] rounds,
        out Dictionary<string, TournamentParticipant> participants,
        out Dictionary<TournamentParticipant, string> slotIds)
    {
        rounds = null;
        if (!TryCreateParticipants(snapshot, out participants, out slotIds))
            return false;

        rounds = new TournamentRound[snapshot.Rounds.Length];
        for (int roundIndex = 0; roundIndex < snapshot.Rounds.Length; roundIndex++)
        {
            TournamentRoundData roundData = snapshot.Rounds[roundIndex];
            TournamentMatch[] matches = new TournamentMatch[roundData.Matches.Length];
            for (int matchIndex = 0; matchIndex < matches.Length; matchIndex++)
            {
                TournamentMatchData matchData = roundData.Matches[matchIndex];
                int participantCount = matchData.Teams.Length * Math.Max(1, matchData.TeamSize);
                var match = new TournamentMatch(
                    participantCount,
                    Math.Max(1, matchData.Teams.Length),
                    Math.Max(1, matchData.NumberOfWinnerParticipants),
                    (TournamentGame.QualificationMode)matchData.QualificationMode);

                var matchParticipants = new List<TournamentParticipant>();
                var teams = new TournamentTeam[matchData.Teams.Length];
                for (int teamIndex = 0; teamIndex < teams.Length; teamIndex++)
                {
                    TournamentTeamData teamData = matchData.Teams[teamIndex];
                    Banner banner = string.IsNullOrEmpty(teamData.BannerCode)
                        ? Banner.CreateOneColoredEmptyBanner(unchecked((int)teamData.TeamColor))
                        : new Banner(teamData.BannerCode);
                    var team = new TournamentTeam(Math.Max(1, matchData.TeamSize), teamData.TeamColor, banner);
                    foreach (string slotId in teamData.ParticipantSlotIds)
                    {
                        if (!participants.TryGetValue(slotId, out var participant))
                            return false;
                        team.AddParticipant(participant);
                        matchParticipants.Add(participant);
                    }
                    teams[teamIndex] = team;
                }

                for (int teamIndex = 0; teamIndex < teams.Length; teamIndex++)
                    match._teams[teamIndex] = teams[teamIndex];
                match._participants.AddRange(matchParticipants);
                var participantMap = participants;
                match._winners = matchData.WinnerSlotIds
                    .Where(participantMap.ContainsKey)
                    .Select(slotId => participantMap[slotId])
                    .ToList();
                match.State = (TournamentMatch.MatchState)matchData.State;
                matches[matchIndex] = match;
            }

            TournamentMatchData firstMatch = roundData.Matches.FirstOrDefault();
            int participantCapacity = roundData.Matches.Sum(roundMatch =>
                roundMatch.Teams.Length * Math.Max(1, roundMatch.TeamSize));
            var round = new TournamentRound(
                Math.Max(1, participantCapacity),
                Math.Max(1, matches.Length),
                Math.Max(1, firstMatch?.Teams.Length ?? 1),
                Math.Max(1, firstMatch?.NumberOfWinnerParticipants ?? 1),
                (TournamentGame.QualificationMode)(firstMatch?.QualificationMode ?? 0));
            round.Matches = matches;
            round.CurrentMatchIndex = Math.Max(0, Math.Min(roundData.CurrentMatchIndex, Math.Max(0, matches.Length - 1)));
            rounds[roundIndex] = round;
        }
        return true;
    }

    internal static bool TryApplyScores(
        TournamentMatch match,
        TournamentMatchData matchData,
        TournamentTeamScoreData[] scoreData)
    {
        if (scoreData == null ||
            matchData?.Teams == null ||
            scoreData.Length != matchData.Teams.Length)
        {
            return false;
        }

        var scoresByTeamId = new Dictionary<string, int>();
        foreach (TournamentTeamScoreData score in scoreData)
        {
            if (score == null ||
                string.IsNullOrEmpty(score.TeamId) ||
                score.Score < 0 ||
                scoresByTeamId.ContainsKey(score.TeamId))
            {
                return false;
            }
            scoresByTeamId.Add(score.TeamId, score.Score);
        }

        var teams = match.Teams.ToArray();
        if (teams.Length != matchData.Teams.Length)
            return false;
        for (int i = 0; i < teams.Length; i++)
        {
            if (!scoresByTeamId.TryGetValue(matchData.Teams[i].TeamId, out var score))
                return false;
            TournamentParticipant scoreHolder = teams[i].Participants.FirstOrDefault();
            if (scoreHolder == null)
                return false;
            scoreHolder.AddScore(score - teams[i].Score);
        }
        return true;
    }

    private static bool TryFindCurrentMatch(
        TournamentSessionSnapshot snapshot,
        TournamentRound[] rounds,
        out int roundIndex,
        out TournamentMatch match)
    {
        for (roundIndex = 0; roundIndex < snapshot.Rounds.Length; roundIndex++)
        {
            for (int matchIndex = 0; matchIndex < snapshot.Rounds[roundIndex].Matches.Length; matchIndex++)
            {
                if (snapshot.Rounds[roundIndex].Matches[matchIndex].MatchId != snapshot.CurrentMatchId)
                    continue;

                rounds[roundIndex].CurrentMatchIndex = matchIndex;
                match = rounds[roundIndex].Matches[matchIndex];
                return true;
            }
        }

        roundIndex = -1;
        match = null;
        return false;
    }

    private static TournamentRoundData[] PackRounds(
        TournamentRound[] rounds,
        Dictionary<TournamentParticipant, string> slotIds)
    {
        var data = new TournamentRoundData[rounds.Length];
        for (int roundIndex = 0; roundIndex < rounds.Length; roundIndex++)
        {
            TournamentRound round = rounds[roundIndex];
            string roundId = $"round:{roundIndex}";
            var matches = new TournamentMatchData[round.Matches.Length];
            for (int matchIndex = 0; matchIndex < round.Matches.Length; matchIndex++)
            {
                TournamentMatch match = round.Matches[matchIndex];
                string matchId = $"{roundId}:match:{matchIndex}";
                TournamentTeam[] nativeTeams = match.Teams.ToArray();
                var teams = new TournamentTeamData[nativeTeams.Length];
                for (int teamIndex = 0; teamIndex < nativeTeams.Length; teamIndex++)
                {
                    TournamentTeam team = nativeTeams[teamIndex];
                    string[] participantSlotIds = team.Participants
                        .Where(slotIds.ContainsKey)
                        .Select(participant => slotIds[participant])
                        .ToArray();
                    bool isWinner = match.Winners.Any(winner => team.Participants.Contains(winner));
                    teams[teamIndex] = new TournamentTeamData(
                        $"{matchId}:team:{teamIndex}",
                        participantSlotIds,
                        team.Score,
                        isWinner,
                        team.TeamColor,
                        team.TeamBanner?.BannerCode);
                }

                matches[matchIndex] = new TournamentMatchData(
                    matchId,
                    roundId,
                    (int)match.State,
                    nativeTeams.FirstOrDefault()?.TeamSize ?? match._teamSize,
                    match._numberOfWinnerParticipants,
                    teams,
                    match.Winners.Where(slotIds.ContainsKey).Select(winner => slotIds[winner]).ToArray(),
                    (int)match.QualificationMode);
            }
            data[roundIndex] = new TournamentRoundData(roundId, roundIndex, round.CurrentMatchIndex, matches);
        }
        return data;
    }

    private sealed class PlayerFilteredFightTournamentGame : FightTournamentGame
    {
        public HashSet<CharacterObject> ExcludedCharacters;

        private PlayerFilteredFightTournamentGame(Town town) : base(town)
        {
        }

        public override bool CanBeAParticipant(CharacterObject character, bool considerSkills)
        {
            if (character == null || ExcludedCharacters?.Contains(character) == true)
                return false;

            return base.CanBeAParticipant(character, considerSkills);
        }
    }
    private static string FindCurrentMatchId(IEnumerable<TournamentRoundData> rounds)
    {
        foreach (TournamentRoundData round in rounds)
        {
            if (round.CurrentMatchIndex < 0 || round.CurrentMatchIndex >= round.Matches.Length)
                continue;

            TournamentMatchData match = round.Matches[round.CurrentMatchIndex];
            if (match.State != (int)TournamentMatch.MatchState.Finished)
                return match.MatchId;
        }
        return null;
    }

    private sealed class TournamentSessionIdentity
    {
    }

    private sealed class FrozenRosterFightTournamentGame : FightTournamentGame
    {
        public MBList<CharacterObject> FrozenCharacters;

        private FrozenRosterFightTournamentGame(Town town) : base(town)
        {
        }

        public override MBList<CharacterObject> GetParticipantCharacters(Settlement settlement, bool includePlayer)
        {
            return new MBList<CharacterObject>(FrozenCharacters);
        }
    }
}
