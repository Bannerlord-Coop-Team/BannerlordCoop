using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments.Data;
using SandBox.Tournaments.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;

namespace Missions.Tournaments;

public interface ITournamentNativeBracketHydrator
{
    void Apply(CoopTournamentBehavior behavior, TournamentSessionSnapshot snapshot);
}

public class TournamentNativeBracketHydrator : ITournamentNativeBracketHydrator
{
    private static readonly FieldInfo MatchTeams = typeof(TournamentMatch).GetField("_teams", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly FieldInfo MatchParticipants = typeof(TournamentMatch).GetField("_participants", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private readonly IObjectManager objectManager;

    public TournamentNativeBracketHydrator(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void Apply(CoopTournamentBehavior behavior, TournamentSessionSnapshot snapshot)
    {
        if (behavior == null) throw new ArgumentNullException(nameof(behavior));
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        var participants = CreateParticipants(snapshot.Contestants);
        behavior._participants = snapshot.Contestants.Select(data => participants[data.SlotId]).ToArray();
        behavior.Rounds = CreateRounds(snapshot, participants);
        behavior.CurrentRoundIndex = FindCurrentRound(snapshot);
        behavior.Winner = ResolveWinner(snapshot, participants);
    }

    private Dictionary<string, TournamentParticipant> CreateParticipants(TournamentContestantData[] contestants)
    {
        var result = new Dictionary<string, TournamentParticipant>();
        foreach (var data in contestants)
        {
            if (!objectManager.TryGetObject(data.CharacterId, out CharacterObject character))
                throw new NullReferenceException(data.CharacterId);

            var descriptor = new UniqueTroopDescriptor(data.DescriptorSeed);
            var participant = new TournamentParticipant(character, descriptor);
            participant.Score = data.Score;
            result.Add(data.SlotId, participant);
        }
        return result;
    }

    private static TournamentRound[] CreateRounds(
        TournamentSessionSnapshot snapshot,
        Dictionary<string, TournamentParticipant> participants)
    {
        var rounds = new TournamentRound[snapshot.Rounds.Length];
        for (int i = 0; i < snapshot.Rounds.Length; i++)
        {
            TournamentRoundData data = snapshot.Rounds[i];
            TournamentMatch[] matches = data.Matches
                .Select(match => CreateMatch(match, participants))
                .ToArray();

            TournamentMatchData first = data.Matches.FirstOrDefault();
            int participantCapacity = data.Matches.Sum(match =>
                match.Teams.Length * Math.Max(1, match.TeamSize));
            var round = new TournamentRound(
                Math.Max(1, participantCapacity),
                Math.Max(1, matches.Length),
                Math.Max(1, first?.Teams.Length ?? 1),
                Math.Max(1, first?.NumberOfWinnerParticipants ?? 1),
                GetQualificationMode(first));
            round.Matches = matches;
            round.CurrentMatchIndex = Math.Max(0, Math.Min(data.CurrentMatchIndex, Math.Max(0, matches.Length - 1)));
            rounds[i] = round;
        }
        return rounds;
    }

    private static TournamentMatch CreateMatch(
        TournamentMatchData data,
        Dictionary<string, TournamentParticipant> participants)
    {
        int participantCount = data.Teams.Length * Math.Max(1, data.TeamSize);
        var match = new TournamentMatch(
            participantCount,
            Math.Max(1, data.Teams.Length),
            Math.Max(1, data.NumberOfWinnerParticipants),
            GetQualificationMode(data));

        TournamentTeam[] teams = data.Teams
            .Select(team => CreateTeam(data, team, participants))
            .ToArray();
        List<TournamentParticipant> matchParticipants = data.Teams
            .SelectMany(team => team.ParticipantSlotIds)
            .Select(slotId => participants[slotId])
            .ToList();
        List<TournamentParticipant> winners = data.WinnerSlotIds
            .Where(participants.ContainsKey)
            .Select(slotId => participants[slotId])
            .ToList();

        MatchTeams.SetValue(match, teams);
        MatchParticipants.SetValue(match, matchParticipants);
        match._winners = winners;
        match.State = (TournamentMatch.MatchState)data.State;
        return match;
    }

    private static TournamentTeam CreateTeam(
        TournamentMatchData matchData,
        TournamentTeamData data,
        Dictionary<string, TournamentParticipant> participants)
    {
        Banner banner = string.IsNullOrEmpty(data.BannerCode)
            ? Banner.CreateOneColoredEmptyBanner(unchecked((int)data.TeamColor))
            : new Banner(data.BannerCode);
        var team = new TournamentTeam(Math.Max(1, matchData.TeamSize), data.TeamColor, banner);
        foreach (string slotId in data.ParticipantSlotIds)
            team.AddParticipant(participants[slotId]);

        TournamentParticipant scoreHolder = team.Participants.FirstOrDefault();
        if (scoreHolder != null && team.Score != data.Score)
            scoreHolder.AddScore(data.Score - team.Score);
        return team;
    }

    private static TournamentGame.QualificationMode GetQualificationMode(TournamentMatchData data)
        => data == null
            ? TournamentGame.QualificationMode.TeamScore
            : (TournamentGame.QualificationMode)data.QualificationMode;

    private static int FindCurrentRound(TournamentSessionSnapshot snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.CurrentMatchId)) return 0;

        for (int i = 0; i < snapshot.Rounds.Length; i++)
            if (snapshot.Rounds[i].Matches.Any(match => match.MatchId == snapshot.CurrentMatchId))
                return i;
        return 0;
    }

    private static TournamentParticipant ResolveWinner(
        TournamentSessionSnapshot snapshot,
        Dictionary<string, TournamentParticipant> participants)
    {
        if (string.IsNullOrEmpty(snapshot.WinnerSlotId)) return null;
        participants.TryGetValue(snapshot.WinnerSlotId, out var winner);
        return winner;
    }
}
