using GameInterface.Services.Tournaments.Data;
using SandBox.Tournaments.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.Tournaments;

public sealed partial class TournamentGameInterface
{
    public bool TrySimulateCurrentMatch(
        TournamentSessionSnapshot snapshot,
        long sequence,
        out TournamentMatchResultData result)
    {
        result = null;
        if (snapshot == null ||
            !TryHydrateRounds(snapshot, out var rounds, out _, out var slotIds) ||
            !TryFindCurrentMatch(snapshot, rounds, out _, out var match) ||
            !objectManager.TryGetObject(snapshot.TownId, out Town town))
        {
            return false;
        }

        TournamentMatchData matchData = snapshot.Rounds
            .SelectMany(round => round.Matches)
            .FirstOrDefault(candidate => candidate.MatchId == snapshot.CurrentMatchId);
        if (matchData == null)
            return false;

        match.Start();
        var fightController = new TournamentFightMissionController(town.Culture);
        fightController.SkipMatch(match);

        TournamentTeam[] teams = match.Teams.ToArray();
        var scores = new TournamentTeamScoreData[teams.Length];
        for (int i = 0; i < teams.Length; i++)
            scores[i] = new TournamentTeamScoreData(matchData.Teams[i].TeamId, teams[i].Score);

        string[] winnerSlots = match.GetWinners()
            .Where(slotIds.ContainsKey)
            .Select(winner => slotIds[winner])
            .ToArray();
        string[] winnerTeams = matchData.Teams
            .Where(team => team.ParticipantSlotIds.Any(winnerSlots.Contains))
            .Select(team => team.TeamId)
            .ToArray();
        result = new TournamentMatchResultData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            sequence,
            winnerTeams,
            winnerSlots,
            scores);
        return true;
    }

    public bool TrySimulateCurrentMatchUnbiased(
        TournamentSessionSnapshot snapshot,
        long sequence,
        out TournamentMatchResultData result)
    {
        result = null;
        TournamentMatchData match = snapshot?.Rounds?
            .SelectMany(round => round.Matches ?? new TournamentMatchData[0])
            .FirstOrDefault(candidate => candidate.MatchId == snapshot.CurrentMatchId);
        if (match?.Teams == null ||
            match.Teams.Length < 2 ||
            Campaign.Current?.Models?.TournamentModel == null)
        {
            return false;
        }

        var rankedTeams = new List<KeyValuePair<TournamentTeamData, float>>();
        foreach (TournamentTeamData team in match.Teams)
        {
            if (team?.ParticipantSlotIds == null || team.ParticipantSlotIds.Length == 0)
                return false;

            float simulationScore = 0f;
            foreach (string slotId in team.ParticipantSlotIds)
            {
                TournamentContestantData contestant = snapshot.Contestants
                    .FirstOrDefault(candidate => candidate.SlotId == slotId);
                if (contestant == null ||
                    !objectManager.TryGetObject(contestant.CharacterId, out CharacterObject character))
                {
                    return false;
                }

                float characterScore = Campaign.Current.Models.TournamentModel
                    .GetTournamentSimulationScore(character);
                simulationScore += characterScore * (0.75f + (0.25f * MBRandom.RandomFloat));
            }
            rankedTeams.Add(new KeyValuePair<TournamentTeamData, float>(team, simulationScore));
        }

        rankedTeams = rankedTeams
            .OrderByDescending(team => team.Value)
            .ToList();
        int teamSize = Math.Max(1, match.TeamSize);
        int winnerParticipantCount = Math.Max(1, match.NumberOfWinnerParticipants);
        int winnerTeamCount = Math.Max(
            1,
            Math.Min(match.Teams.Length, (winnerParticipantCount + teamSize - 1) / teamSize));
        TournamentTeamData[] winnerTeams = rankedTeams
            .Take(winnerTeamCount)
            .Select(team => team.Key)
            .ToArray();
        string[] winnerTeamIds = winnerTeams.Select(team => team.TeamId).ToArray();
        string[] winnerSlotIds = winnerTeams
            .SelectMany(team => team.ParticipantSlotIds)
            .Take(winnerParticipantCount)
            .ToArray();

        int winningScoreBase = match.Teams.Max(team => team.Score) + winnerTeamCount;
        var scores = new TournamentTeamScoreData[match.Teams.Length];
        for (int i = 0; i < match.Teams.Length; i++)
        {
            TournamentTeamData team = match.Teams[i];
            int winnerIndex = Array.FindIndex(winnerTeams, winner => winner.TeamId == team.TeamId);
            int score = winnerIndex < 0
                ? team.Score
                : winningScoreBase - winnerIndex;
            scores[i] = new TournamentTeamScoreData(team.TeamId, score);
        }

        result = new TournamentMatchResultData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            sequence,
            winnerTeamIds,
            winnerSlotIds,
            scores);
        return true;
    }

    public bool TryGetBetQuote(
        TournamentSessionSnapshot snapshot,
        Hero hero,
        string slotId,
        out TournamentBetQuote quote)
    {
        quote = null;
        if (snapshot == null || hero == null || string.IsNullOrEmpty(slotId))
            return false;

        TournamentRoundData currentRound = snapshot.Rounds.FirstOrDefault(round =>
            round.Matches.Any(match => match.MatchId == snapshot.CurrentMatchId));
        if (currentRound == null)
            return false;

        List<KeyValuePair<Hero, int>> leaderboard = Campaign.Current?.TournamentManager?.GetLeaderboard()
            ?? new List<KeyValuePair<Hero, int>>();
        int highestWins = leaderboard.Count == 0 ? 0 : leaderboard.Max(entry => entry.Value);
        if (!TryCalculateRoundPowers(
                snapshot,
                currentRound,
                slotId,
                leaderboard,
                highestWins,
                out float playerTeamPower,
                out float currentMatchOpponentPower,
                out float totalRoundPower))
            return false;

        int heroWins = GetTournamentWins(leaderboard, hero);
        float heroPower = 30f + hero.Level + Math.Max(0, (heroWins * 12) - (highestWins * 2));
        float odd = TournamentBettingMath.CalculateOdd(
            heroPower,
            playerTeamPower,
            currentMatchOpponentPower,
            totalRoundPower);
        int maximumBet = TournamentBettingMath.CalculateMaximumBet(
            hero.GetPerkValue(DefaultPerks.Roguery.DeepPockets),
            DefaultPerks.Roguery.DeepPockets.PrimaryBonus);
        quote = new TournamentBetQuote(maximumBet, odd);
        return true;
    }

    private bool TryCalculateRoundPowers(
        TournamentSessionSnapshot snapshot,
        TournamentRoundData currentRound,
        string slotId,
        List<KeyValuePair<Hero, int>> leaderboard,
        int highestWins,
        out float playerTeamPower,
        out float currentMatchOpponentPower,
        out float totalRoundPower)
    {
        playerTeamPower = 0f;
        currentMatchOpponentPower = 0f;
        totalRoundPower = 0f;
        bool playerTeamFound = false;
        foreach (TournamentMatchData match in currentRound.Matches)
        {
            foreach (TournamentTeamData team in match.Teams)
            {
                if (!TryCalculateTeamPower(snapshot, team, slotId, leaderboard, highestWins, out float teamPower))
                    return false;

                totalRoundPower += teamPower;
                bool isCurrentMatch = match.MatchId == snapshot.CurrentMatchId;
                bool isPlayerTeam = team.ParticipantSlotIds.Contains(slotId);
                if (isCurrentMatch && isPlayerTeam)
                {
                    playerTeamPower = teamPower;
                    playerTeamFound = true;
                }
                else if (isCurrentMatch)
                {
                    currentMatchOpponentPower += teamPower;
                }
            }
        }
        return playerTeamFound;
    }

    private bool TryCalculateTeamPower(
        TournamentSessionSnapshot snapshot,
        TournamentTeamData team,
        string playerSlotId,
        List<KeyValuePair<Hero, int>> leaderboard,
        int highestWins,
        out float teamPower)
    {
        teamPower = 0f;
        foreach (string participantSlotId in team.ParticipantSlotIds)
        {
            if (participantSlotId == playerSlotId)
                continue;
            TournamentContestantData contestant = snapshot.Contestants
                .FirstOrDefault(candidate => candidate.SlotId == participantSlotId);
            if (contestant == null ||
                !objectManager.TryGetObject(contestant.CharacterId, out CharacterObject character))
                return false;

            int wins = character.IsHero
                ? GetTournamentWins(leaderboard, character.HeroObject)
                : 0;
            teamPower += character.Level + Math.Max(0, (wins * 8) - (highestWins * 2));
        }
        return true;
    }
    private static int GetTournamentWins(List<KeyValuePair<Hero, int>> leaderboard, Hero hero)
    {
        if (hero == null)
            return 0;
        for (int i = 0; i < leaderboard.Count; i++)
        {
            if (leaderboard[i].Key == hero)
                return leaderboard[i].Value;
        }
        return 0;
    }
}
