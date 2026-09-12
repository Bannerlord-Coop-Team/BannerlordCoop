namespace GameInterface.Services.Tournaments.UI;

/// <summary>
/// Detects authoritative transitions into and out of tournament combat without starting native match logic locally.
/// </summary>
internal sealed class TournamentMatchPresentation
{
    private bool isMatchActive;

    public TournamentMatchPresentation(bool initiallyActive)
    {
        isMatchActive = initiallyActive;
    }

    public bool Observe(bool active)
    {
        bool enteredMatch = active && !isMatchActive;
        isMatchActive = active;
        return enteredMatch;
    }
}
