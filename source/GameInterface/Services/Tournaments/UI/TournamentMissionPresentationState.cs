using GameInterface.Services.Tournaments.Data;

namespace GameInterface.Services.Tournaments.UI;

internal readonly struct TournamentMissionPresentationState
{
    public readonly bool ShouldShowUI;
    public readonly bool UseCustomCamera;
    public readonly bool CaptureInput;

    private TournamentMissionPresentationState(bool shouldShowUI)
    {
        ShouldShowUI = shouldShowUI;
        UseCustomCamera = shouldShowUI;
        CaptureInput = shouldShowUI;
    }

    public static TournamentMissionPresentationState From(TournamentSessionSnapshot snapshot)
        => new(snapshot != null &&
               snapshot.Phase != TournamentSessionPhase.LiveMatch);
}
