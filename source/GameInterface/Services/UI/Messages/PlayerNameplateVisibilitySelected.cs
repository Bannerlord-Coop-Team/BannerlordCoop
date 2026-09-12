using Common.Messaging;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;

namespace GameInterface.Services.UI.Messages;

public readonly struct PlayerNameplateVisibilitySelected : IEvent
{
    public readonly PlayerNameplatesDisplayMode DisplayMode;

    public PlayerNameplateVisibilitySelected(PlayerNameplatesDisplayMode displayMode)
    {
        DisplayMode = displayMode;
    }
}
