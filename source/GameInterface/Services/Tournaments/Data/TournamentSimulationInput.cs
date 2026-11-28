namespace GameInterface.Services.Tournaments.Data;

public sealed record TournamentSimulationInput(
    TournamentMatchData Match,
    TournamentTeamData[] EligibleTeams,
    int WinnerTeamCount,
    int WinnerParticipantCount);
