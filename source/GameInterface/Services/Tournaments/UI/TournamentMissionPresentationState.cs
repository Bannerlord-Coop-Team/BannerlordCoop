using GameInterface.Services.Tournaments.Data;

namespace GameInterface.Services.Tournaments.UI;

internal readonly struct TournamentMissionPresentationState
{
    public readonly bool ShouldShowUI;

    private TournamentMissionPresentationState(bool shouldShowUI)
    {
        ShouldShowUI = shouldShowUI;
    }

    public static TournamentMissionPresentationState From(TournamentSessionSnapshot snapshot)
        => new(snapshot != null &&
               snapshot.Phase != TournamentSessionPhase.LiveMatch);
}
