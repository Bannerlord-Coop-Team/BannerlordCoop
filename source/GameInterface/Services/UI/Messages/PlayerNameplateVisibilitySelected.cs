using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

public readonly struct PlayerNameplateVisibilitySelected : IEvent
{
    public readonly bool ShowPlayerNameplates;

    public PlayerNameplateVisibilitySelected(bool showPlayerNameplates)
    {
        ShowPlayerNameplates = showPlayerNameplates;
    }
}
