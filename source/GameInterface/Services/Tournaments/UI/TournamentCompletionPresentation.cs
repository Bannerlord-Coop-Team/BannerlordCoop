using System;

namespace GameInterface.Services.Tournaments.UI;

internal sealed class TournamentCompletionPresentation
{
    private bool pending;

    public bool IsPresented { get; private set; }

    public void Observe(bool isCompleted)
    {
        if (isCompleted && !IsPresented)
            pending = true;
    }

    public bool TryPresent(Action present)
    {
        if (!pending || IsPresented)
            return false;

        pending = false;
        IsPresented = true;
        present();
        return true;
    }
}
