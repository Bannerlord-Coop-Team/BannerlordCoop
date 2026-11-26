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
        TournamentMatchData match,
        TournamentTeamData[] eligibleTeams,
        int winnerTeamCount,
        long sequence,
        out TournamentMatchResultData result)
    {
        result = null;
        if (Campaign.Current?.Models?.TournamentModel == null)
            return false;

        var rankedTeams = new List<KeyValuePair<TournamentTeamData, float>>();
        var contestants = snapshot.Contestants.ToDictionary(contestant => contestant.SlotId);
        foreach (TournamentTeamData team in eligibleTeams)
        {
            float simulationScore = 0f;
            foreach (string slotId in team.ParticipantSlotIds)
            {
                if (!contestants.TryGetValue(slotId, out var contestant) ||
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

        string[] winnerTeamIds = rankedTeams
            .OrderByDescending(team => team.Value)
            .Take(winnerTeamCount)
            .Select(team => team.Key.TeamId)
            .ToArray();
        result = BuildSimulatedMatchResult(snapshot, match, winnerTeamIds, sequence);
        return true;
    }

    public TournamentMatchResultData BuildSimulatedMatchResult(
        TournamentSessionSnapshot snapshot,
        TournamentMatchData match,
        string[] winnerTeamIds,
        long sequence)
    {
        string[] winnerSlotIds = winnerTeamIds
            .SelectMany(teamId => match.Teams.Single(team => team.TeamId == teamId).ParticipantSlotIds)
            .Take(Math.Max(1, match.NumberOfWinnerParticipants))
            .ToArray();

        var scores = new TournamentTeamScoreData[match.Teams.Length];
        for (int i = 0; i < match.Teams.Length; i++)
        {
            TournamentTeamData team = match.Teams[i];
            int winnerIndex = Array.IndexOf(winnerTeamIds, team.TeamId);
            // Each match starts at zero. Losers score zero, and winners rank down from the number of winner teams.
            int score = winnerIndex < 0
                ? 0
                : winnerTeamIds.Length - winnerIndex;
            scores[i] = new TournamentTeamScoreData(team.TeamId, score);
        }

        return new TournamentMatchResultData(
            snapshot.SessionId,
            snapshot.CurrentMatchId,
            snapshot.Revision,
            snapshot.BracketRevision,
            sequence,
            winnerTeamIds,
            winnerSlotIds,
            scores);
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
