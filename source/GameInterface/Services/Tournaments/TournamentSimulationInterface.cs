using Common.Util;
using GameInterface.Services.Tournaments.Data;
using SandBox.Tournaments.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Tournaments;

public sealed partial class TournamentGameInterface
{
    public bool TrySimulateCurrentMatchUnbiased(
        TournamentSessionSnapshot snapshot,
        long sequence,
        out TournamentMatchResultData result)
    {
        result = null;
        TournamentMatchData match = snapshot?.Rounds?
            .SelectMany(round => round.Matches ?? Array.Empty<TournamentMatchData>())
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
        if (snapshot == null ||
            hero?.CharacterObject == null ||
            string.IsNullOrEmpty(slotId) ||
            Game.Current == null ||
            !TryHydrateRounds(snapshot, out var rounds, out var participants, out _) ||
            !TryFindCurrentMatch(snapshot, rounds, out var roundIndex, out _) ||
            !participants.TryGetValue(slotId, out var playerParticipant) ||
            playerParticipant.Character != hero.CharacterObject ||
            !snapshot.Rounds[roundIndex].Matches
                .First(match => match.MatchId == snapshot.CurrentMatchId)
                .Teams.Any(team => team.ParticipantSlotIds.Contains(slotId)))
        {
            return false;
        }

        var behavior = ObjectHelper.SkipConstructor<TournamentBehavior>();
        behavior.Rounds = rounds;
        behavior._participants = participants.Values.ToArray();
        behavior.CurrentRoundIndex = roundIndex;
        behavior.IsPlayerParticipating = true;
        behavior.IsPlayerEliminated = false;

        // CalculateBet has no player parameter, so scope its single-player global to the authenticated bettor.
        BasicCharacterObject previousPlayerTroop = Game.Current.PlayerTroop;
        try
        {
            Game.Current.PlayerTroop = hero.CharacterObject;
            behavior.CalculateBet();
            quote = new TournamentBetQuote(behavior.GetMaximumBet(), behavior.BetOdd);
            return true;
        }
        finally
        {
            Game.Current.PlayerTroop = previousPlayerTroop;
        }
    }
}
